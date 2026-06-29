using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class TryOnService
{
    private readonly IBodyReferencePhotoRepository _bodyPhotos;
    private readonly IOutfitRepository _outfits;
    private readonly ITryOnJobRepository _jobs;
    private readonly ITryOnJobQueue _queue;
    private readonly ITryOnProvider _provider;
    private readonly TryOnCostEstimator _estimator;
    private readonly IClock _clock;
    private readonly IStoredPhotoUrlRefresher? _photoUrls;
    private readonly ITryOnOutputStorage? _tryOnOutputStorage;
    private readonly IUserAccountRepository? _users;
    private readonly TimeSpan _outputRetention = TimeSpan.FromDays(30);
    private const string NoBodyReferenceIdentity = "body:none";

    public TryOnService(
        IBodyReferencePhotoRepository bodyPhotos,
        IOutfitRepository outfits,
        ITryOnJobRepository jobs,
        ITryOnJobQueue queue,
        ITryOnProvider provider,
        TryOnCostEstimator estimator,
        IClock clock,
        IStoredPhotoUrlRefresher? photoUrls = null,
        ITryOnOutputStorage? tryOnOutputStorage = null,
        IUserAccountRepository? users = null)
    {
        _bodyPhotos = bodyPhotos;
        _outfits = outfits;
        _jobs = jobs;
        _queue = queue;
        _provider = provider;
        _estimator = estimator;
        _clock = clock;
        _photoUrls = photoUrls;
        _tryOnOutputStorage = tryOnOutputStorage;
        _users = users ?? bodyPhotos as IUserAccountRepository;
    }

    public TryOnCostEstimate Estimate(string userId, Guid outfitId, TryOnMode mode, string? bodyReferencePhotoUrl, Guid? sourceBodyPhotoId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var normalizedBodyPhotoUrl = ResolveBodyReferencePhotoUrl(normalizedUserId, mode, bodyReferencePhotoUrl, sourceBodyPhotoId);
        var outfit = RefreshOutfitPhotoUrls(_outfits.GetOutfitByUser(normalizedUserId, outfitId)
            ?? throw new InvalidOperationException("Outfit was not found."));
        var bodyIdentity = BodyReferenceIdentity(normalizedUserId, sourceBodyPhotoId, normalizedBodyPhotoUrl);
        var userGender = UserGenderFor(normalizedUserId);
        var cacheProbe = _estimator.Estimate(outfit, new TryOnEstimateInput(
            mode,
            _provider.Name,
            bodyIdentity,
            _provider.Capabilities.SettingsHash,
            hasCachedResult: false,
            creditsPerRun: _provider.Capabilities.CreditsPerRun,
            userGender: userGender));
        var cached = _jobs.FindSucceededTryOnJobByCacheKey(normalizedUserId, cacheProbe.CacheKey);

        var estimate = _estimator.Estimate(outfit, new TryOnEstimateInput(
            mode,
            _provider.Name,
            bodyIdentity,
            _provider.Capabilities.SettingsHash,
            hasCachedResult: cached is not null,
            creditsPerRun: _provider.Capabilities.CreditsPerRun,
            userGender: userGender));
        return ApplyUserProfileAvailability(ApplyProviderAvailability(estimate), normalizedUserId);
    }

    public async Task<TryOnJob> StartAsync(
        string userId,
        Guid outfitId,
        string? bodyReferencePhotoUrl,
        bool consentAccepted,
        TryOnMode tryOnMode,
        int confirmedCredits,
        string confirmedCacheKey,
        Guid? sourceBodyPhotoId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var normalizedBodyPhotoUrl = ResolveBodyReferencePhotoUrl(normalizedUserId, tryOnMode, bodyReferencePhotoUrl, sourceBodyPhotoId);
        var estimate = Estimate(normalizedUserId, outfitId, tryOnMode, normalizedBodyPhotoUrl, sourceBodyPhotoId);
        if (!estimate.IsAvailable)
        {
            throw new InvalidOperationException(estimate.Summary);
        }

        if (confirmedCredits != estimate.EstimatedCredits)
        {
            throw new InvalidOperationException("Confirmed credits do not match the current try-on estimate. Refresh the estimate before generating.");
        }

        if (!string.Equals(confirmedCacheKey, estimate.CacheKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Confirmed estimate is stale. Refresh the estimate before generating.");
        }

        if (estimate.RequiresAi && !consentAccepted)
        {
            throw new InvalidOperationException("Explicit consent is required before sending photos to an AI provider.");
        }

        if (!_provider.Capabilities.SupportedModes.Contains(tryOnMode) && estimate.RequiresAi)
        {
            throw new InvalidOperationException($"{_provider.Name} does not support {tryOnMode}.");
        }

        var now = _clock.UtcNow;
        var started = new TryOnJob(
            Guid.NewGuid(),
            normalizedUserId,
            outfitId,
            normalizedBodyPhotoUrl,
            tryOnMode == TryOnMode.SequentialOutfitTryOn,
            TryOnStatus.Queued,
            null,
            null,
            null,
            now,
            now)
        {
            ConsentAcceptedAt = estimate.RequiresAi ? now : null,
            ProviderName = _provider.Name,
            SourceBodyPhotoId = sourceBodyPhotoId,
            RetentionUntil = now.Add(_outputRetention),
            IsDeleted = false,
            TryOnMode = tryOnMode,
            ConfirmedCredits = estimate.EstimatedCredits,
            CacheKey = estimate.CacheKey,
            ProviderSettingsHash = _provider.Capabilities.SettingsHash
        };

        if (!estimate.RequiresAi)
        {
            var completed = started with
            {
                Status = TryOnStatus.Succeeded,
                UpdatedAt = now
            };
            _jobs.AddTryOnJob(completed);
            return completed;
        }

        var cached = _jobs.FindSucceededTryOnJobByCacheKey(normalizedUserId, estimate.CacheKey);
        if (cached is not null)
        {
            var cachedOutputImageUrl = !string.IsNullOrWhiteSpace(cached.OutputImageUrl)
                ? await StoreTryOnOutputAsync(started, cached.OutputImageUrl, cancellationToken)
                : cached.OutputImageUrl;
            var cacheHit = started with
            {
                Status = TryOnStatus.Succeeded,
                ProviderJobId = cached.ProviderJobId,
                ProviderRequestId = cached.ProviderRequestId,
                OutputImageUrl = cachedOutputImageUrl,
                ServedFromCache = true,
                SourceCachedJobId = cached.Id,
                UpdatedAt = now
            };
            _jobs.AddTryOnJob(cacheHit);
            if (!string.IsNullOrWhiteSpace(cacheHit.OutputImageUrl))
            {
                var outfit = _outfits.GetOutfitByUser(normalizedUserId, outfitId);
                if (outfit is not null)
                {
                    _outfits.UpdateOutfit(outfit with { PersonPreviewUrl = cacheHit.OutputImageUrl });
                }
            }

            return cacheHit;
        }

        _jobs.AddTryOnJob(started);
        await _queue.EnqueueAsync(started.Id, cancellationToken);
        return started;
    }

    public async Task ProcessQueuedJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var queued = _jobs.GetTryOnJobById(jobId);
        if (queued is null || queued.Status != TryOnStatus.Queued)
        {
            return;
        }

        var outfit = _outfits.GetOutfitByUser(queued.UserId, queued.OutfitId);
        if (outfit is null)
        {
            _jobs.UpdateTryOnJob(queued with
            {
                Status = TryOnStatus.Failed,
                Error = "Outfit was not found.",
                UpdatedAt = _clock.UtcNow
            });
            return;
        }

        outfit = RefreshOutfitPhotoUrls(outfit);
        var bodyReferencePhotoUrl = _photoUrls?.RefreshBodyReferencePhotoUrl(queued.BodyReferencePhotoUrl)
            ?? queued.BodyReferencePhotoUrl;
        var processing = queued with
        {
            Status = TryOnStatus.Processing,
            UpdatedAt = _clock.UtcNow
        };
        _jobs.UpdateTryOnJob(processing);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var estimate = _estimator.Estimate(outfit, new TryOnEstimateInput(
                queued.TryOnMode,
                queued.ProviderName ?? _provider.Name,
                BodyReferenceIdentity(queued.UserId, queued.SourceBodyPhotoId, bodyReferencePhotoUrl),
                queued.ProviderSettingsHash ?? _provider.Capabilities.SettingsHash,
                hasCachedResult: false,
                creditsPerRun: _provider.Capabilities.CreditsPerRun,
                userGender: UserGenderFor(queued.UserId)));
            if (estimate.RequiresAi && _users?.GetUserById(queued.UserId) is { Gender: null })
            {
                throw new InvalidOperationException("Set gender in account settings before using AI try-on.");
            }

            var visualOnlyItems = queued.TryOnMode == TryOnMode.ExperimentalCompositeTryOn
                ? estimate.VisualOnlyItems
                : Array.Empty<OutfitItem>();
            var bodyTryOnItems = _photoUrls is null
                ? estimate.BodyTryOnItems
                : estimate.BodyTryOnItems
                    .Select(item => item with { ThumbnailUrl = _photoUrls.RefreshGarmentImageUrl(item.ThumbnailUrl) })
                    .ToList();

            var generation = _provider.Generate(new TryOnProviderRequest(
                queued.UserId,
                outfit.Id,
                queued.TryOnMode,
                bodyReferencePhotoUrl,
                bodyTryOnItems,
                visualOnlyItems,
                new TryOnGenerationSettings(
                    _provider.Capabilities.ModelName,
                    _provider.Capabilities.ProviderMode,
                    _provider.Capabilities.SettingsHash))
            {
                UserGender = UserGenderFor(queued.UserId)
            });
            var outputImageUrl = await StoreTryOnOutputAsync(processing, generation.OutputImageUrl, cancellationToken);
            var completed = processing with
            {
                Status = TryOnStatus.Succeeded,
                ProviderJobId = generation.ProviderJobId,
                ProviderRequestId = generation.ProviderJobId,
                OutputImageUrl = outputImageUrl,
                UpdatedAt = _clock.UtcNow
            };

            _jobs.UpdateTryOnJob(completed);
            _outfits.UpdateOutfit(outfit with { PersonPreviewUrl = outputImageUrl });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            var failed = processing with
            {
                Status = TryOnStatus.Failed,
                Error = ex.Message,
                UpdatedAt = _clock.UtcNow
            };
            _jobs.UpdateTryOnJob(failed);
        }

        return;
    }

    private Task<string> StoreTryOnOutputAsync(TryOnJob job, string outputImageUrl, CancellationToken cancellationToken)
    {
        if (_tryOnOutputStorage is null)
        {
            return Task.FromResult(outputImageUrl);
        }

        return _tryOnOutputStorage.StoreAsync(
            job.Id,
            outputImageUrl,
            job.RetentionUntil ?? _clock.UtcNow.Add(_outputRetention),
            cancellationToken);
    }

    private string BodyReferenceIdentity(string userId, Guid? sourceBodyPhotoId, string bodyReferencePhotoUrl)
    {
        if (sourceBodyPhotoId is { } photoId)
        {
            var photo = _bodyPhotos.GetBodyReferencePhotoByUser(userId, photoId)
                ?? throw new InvalidOperationException("Body reference photo was not found.");
            return $"body:{photo.Id:N}";
        }

        if (string.IsNullOrWhiteSpace(bodyReferencePhotoUrl))
        {
            return NoBodyReferenceIdentity;
        }

        return $"url:{bodyReferencePhotoUrl.Trim()}";
    }

    private string ResolveBodyReferencePhotoUrl(string userId, TryOnMode mode, string? bodyReferencePhotoUrl, Guid? sourceBodyPhotoId)
    {
        if (sourceBodyPhotoId is { } photoId)
        {
            var photo = _bodyPhotos.GetBodyReferencePhotoByUser(userId, photoId)
                ?? throw new InvalidOperationException("Body reference photo was not found.");
            return _photoUrls?.RefreshBodyReferencePhotoUrl(photo.ImageUrl) ?? photo.ImageUrl;
        }

        var normalized = NormalizeBodyReferencePhotoUrl(mode, bodyReferencePhotoUrl);
        return string.IsNullOrWhiteSpace(normalized)
            ? normalized
            : _photoUrls?.RefreshBodyReferencePhotoUrl(normalized) ?? normalized;
    }

    private Outfit RefreshOutfitPhotoUrls(Outfit outfit)
    {
        if (_photoUrls is null)
        {
            return outfit;
        }

        return outfit with
        {
            Items = outfit.Items
                .Select(item => item with { ThumbnailUrl = _photoUrls.RefreshGarmentThumbnailUrl(item.ThumbnailUrl) })
                .ToArray()
        };
    }

    private static string NormalizeBodyReferencePhotoUrl(TryOnMode mode, string? bodyReferencePhotoUrl)
    {
        if (mode == TryOnMode.ClothesOnlyPreview && string.IsNullOrWhiteSpace(bodyReferencePhotoUrl))
        {
            return string.Empty;
        }

        return InputGuard.RequireText(bodyReferencePhotoUrl ?? string.Empty, "Body reference photo URL");
    }

    private UserGender? UserGenderFor(string userId)
    {
        return _users?.GetUserById(userId)?.Gender;
    }

    private TryOnCostEstimate ApplyUserProfileAvailability(TryOnCostEstimate estimate, string userId)
    {
        if (!estimate.RequiresAi)
        {
            return estimate;
        }

        var user = _users?.GetUserById(userId);
        if (user is null || user.Gender is not null)
        {
            return estimate;
        }

        const string message = "Set gender in account settings before using AI try-on.";
        return estimate with
        {
            IsAvailable = false,
            Summary = message,
            Warnings = estimate.Warnings.Concat(new[] { message }).ToArray()
        };
    }

    private TryOnCostEstimate ApplyProviderAvailability(TryOnCostEstimate estimate)
    {
        if (!estimate.RequiresAi || _provider.Capabilities.SupportedModes.Contains(estimate.Mode))
        {
            return estimate;
        }

        var message = $"{_provider.Name} does not support {estimate.Mode}.";
        return estimate with
        {
            IsAvailable = false,
            Summary = message,
            Warnings = estimate.Warnings.Concat(new[] { message }).ToArray()
        };
    }

    public TryOnJob? GetJob(string userId, Guid jobId)
    {
        return _jobs.GetTryOnJobByUser(InputGuard.NormalizeUserId(userId), jobId);
    }

    public bool DeleteOutput(string userId, Guid jobId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var job = _jobs.GetTryOnJobByUser(normalizedUserId, jobId);
        if (job is null)
        {
            return false;
        }

        var outputImageUrl = job.OutputImageUrl;
        if (!string.IsNullOrWhiteSpace(outputImageUrl))
        {
            _tryOnOutputStorage?.DeleteOutput(outputImageUrl);

            var outfit = _outfits.GetOutfitByUser(normalizedUserId, job.OutfitId);
            if (outfit?.PersonPreviewUrl == outputImageUrl)
            {
                _outfits.UpdateOutfit(outfit with { PersonPreviewUrl = null });
            }
        }

        _jobs.UpdateTryOnJob(job with
        {
            OutputImageUrl = null,
            IsDeleted = true,
            UpdatedAt = _clock.UtcNow
        });
        return true;
    }

    public bool DeleteActiveOutfitOutput(string userId, Guid outfitId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var outfit = _outfits.GetOutfitByUser(normalizedUserId, outfitId);
        if (outfit is null || string.IsNullOrWhiteSpace(outfit.PersonPreviewUrl))
        {
            return false;
        }

        var activeOutputUrl = outfit.PersonPreviewUrl;
        var matchingJob = _jobs.ListTryOnJobsByUser(normalizedUserId)
            .FirstOrDefault(job => job.OutfitId == outfitId && job.OutputImageUrl == activeOutputUrl && !job.IsDeleted);
        if (matchingJob is not null)
        {
            return DeleteOutput(normalizedUserId, matchingJob.Id);
        }

        _tryOnOutputStorage?.DeleteOutput(activeOutputUrl);
        _outfits.UpdateOutfit(outfit with { PersonPreviewUrl = null });
        return true;
    }

    public int PurgeAiOutputs(string userId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var jobs = _jobs.ListTryOnJobsByUser(normalizedUserId);
        var purged = 0;
        foreach (var job in jobs.Where(job => !job.IsDeleted || job.OutputImageUrl is not null))
        {
            if (!string.IsNullOrWhiteSpace(job.OutputImageUrl))
            {
                _tryOnOutputStorage?.DeleteOutput(job.OutputImageUrl);
            }

            _jobs.UpdateTryOnJob(job with
            {
                OutputImageUrl = null,
                IsDeleted = true,
                UpdatedAt = _clock.UtcNow
            });
            purged++;
        }

        return purged;
    }
}
