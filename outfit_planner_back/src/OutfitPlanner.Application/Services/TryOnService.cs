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
    private readonly TimeSpan _outputRetention = TimeSpan.FromDays(30);

    public TryOnService(
        IBodyReferencePhotoRepository bodyPhotos,
        IOutfitRepository outfits,
        ITryOnJobRepository jobs,
        ITryOnJobQueue queue,
        ITryOnProvider provider,
        TryOnCostEstimator estimator,
        IClock clock)
    {
        _bodyPhotos = bodyPhotos;
        _outfits = outfits;
        _jobs = jobs;
        _queue = queue;
        _provider = provider;
        _estimator = estimator;
        _clock = clock;
    }

    public TryOnCostEstimate Estimate(string userId, Guid outfitId, TryOnMode mode, string bodyReferencePhotoUrl, Guid? sourceBodyPhotoId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var normalizedBodyPhotoUrl = InputGuard.RequireText(bodyReferencePhotoUrl, "Body reference photo URL");
        var outfit = _outfits.GetOutfitByUser(normalizedUserId, outfitId)
            ?? throw new InvalidOperationException("Outfit was not found.");
        var bodyIdentity = BodyReferenceIdentity(normalizedUserId, sourceBodyPhotoId, normalizedBodyPhotoUrl);
        var cacheProbe = _estimator.Estimate(outfit, new TryOnEstimateInput(
            mode,
            _provider.Name,
            bodyIdentity,
            _provider.Capabilities.SettingsHash,
            hasCachedResult: false));
        var cached = _jobs.FindSucceededTryOnJobByCacheKey(normalizedUserId, cacheProbe.CacheKey);

        return _estimator.Estimate(outfit, new TryOnEstimateInput(
            mode,
            _provider.Name,
            bodyIdentity,
            _provider.Capabilities.SettingsHash,
            cached is not null));
    }

    public Task<TryOnJob> StartAsync(
        string userId,
        Guid outfitId,
        string bodyReferencePhotoUrl,
        bool consentAccepted,
        bool sequentialFlowEnabled = false,
        Guid? sourceBodyPhotoId = null,
        CancellationToken cancellationToken = default)
    {
        var mode = sequentialFlowEnabled ? TryOnMode.SequentialOutfitTryOn : TryOnMode.SingleGarmentTryOn;
        var estimate = Estimate(userId, outfitId, mode, bodyReferencePhotoUrl, sourceBodyPhotoId);
        return StartAsync(userId, outfitId, bodyReferencePhotoUrl, consentAccepted, mode, estimate.EstimatedCredits, estimate.CacheKey, sourceBodyPhotoId, cancellationToken);
    }

    public async Task<TryOnJob> StartAsync(
        string userId,
        Guid outfitId,
        string bodyReferencePhotoUrl,
        bool consentAccepted,
        TryOnMode tryOnMode,
        int confirmedCredits,
        string confirmedCacheKey,
        Guid? sourceBodyPhotoId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var normalizedBodyPhotoUrl = InputGuard.RequireText(bodyReferencePhotoUrl, "Body reference photo URL");
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
            var cacheHit = started with
            {
                Status = TryOnStatus.Succeeded,
                ProviderJobId = cached.ProviderJobId,
                ProviderRequestId = cached.ProviderRequestId,
                OutputImageUrl = cached.OutputImageUrl,
                ServedFromCache = true,
                SourceCachedJobId = cached.Id,
                UpdatedAt = now
            };
            _jobs.AddTryOnJob(cacheHit);
            return cacheHit;
        }

        _jobs.AddTryOnJob(started);
        await _queue.EnqueueAsync(started.Id, cancellationToken);
        return started;
    }

    public TryOnJob Start(string userId, Guid outfitId, string bodyReferencePhotoUrl, bool consentAccepted, bool sequentialFlowEnabled = false)
    {
        var mode = sequentialFlowEnabled ? TryOnMode.SequentialOutfitTryOn : TryOnMode.SingleGarmentTryOn;
        var estimate = Estimate(userId, outfitId, mode, bodyReferencePhotoUrl, null);
        return StartAsync(userId, outfitId, bodyReferencePhotoUrl, consentAccepted, mode, estimate.EstimatedCredits, estimate.CacheKey)
            .GetAwaiter()
            .GetResult();
    }

    public Task ProcessQueuedJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var queued = _jobs.GetTryOnJobById(jobId);
        if (queued is null || queued.Status != TryOnStatus.Queued)
        {
            return Task.CompletedTask;
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
            return Task.CompletedTask;
        }

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
                BodyReferenceIdentity(queued.UserId, queued.SourceBodyPhotoId, queued.BodyReferencePhotoUrl),
                queued.ProviderSettingsHash ?? _provider.Capabilities.SettingsHash,
                hasCachedResult: false));
            var generation = _provider.Generate(new TryOnProviderRequest(
                queued.UserId,
                outfit.Id,
                queued.TryOnMode,
                queued.BodyReferencePhotoUrl,
                estimate.BodyTryOnItems,
                estimate.VisualOnlyItems,
                new TryOnGenerationSettings(
                    _provider.Capabilities.ModelName,
                    _provider.Capabilities.ProviderMode,
                    _provider.Capabilities.SettingsHash)));
            var completed = processing with
            {
                Status = TryOnStatus.Succeeded,
                ProviderJobId = generation.ProviderJobId,
                ProviderRequestId = generation.ProviderJobId,
                OutputImageUrl = generation.OutputImageUrl,
                UpdatedAt = _clock.UtcNow
            };

            _jobs.UpdateTryOnJob(completed);
            _outfits.UpdateOutfit(outfit with { PersonPreviewUrl = generation.OutputImageUrl });
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

        return Task.CompletedTask;
    }

    private string BodyReferenceIdentity(string userId, Guid? sourceBodyPhotoId, string bodyReferencePhotoUrl)
    {
        if (sourceBodyPhotoId is { } photoId)
        {
            var photo = _bodyPhotos.GetBodyReferencePhotoByUser(userId, photoId)
                ?? throw new InvalidOperationException("Body reference photo was not found.");
            return $"body:{photo.Id:N}";
        }

        return $"url:{bodyReferencePhotoUrl.Trim()}";
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

        _jobs.UpdateTryOnJob(job with
        {
            OutputImageUrl = null,
            IsDeleted = true,
            UpdatedAt = _clock.UtcNow
        });
        return true;
    }

    public int PurgeAiOutputs(string userId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var jobs = _jobs.ListTryOnJobsByUser(normalizedUserId);
        var purged = 0;
        foreach (var job in jobs.Where(job => !job.IsDeleted || job.OutputImageUrl is not null))
        {
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
