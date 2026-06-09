using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class TryOnService
{
    private readonly IOutfitRepository _outfits;
    private readonly ITryOnJobRepository _jobs;
    private readonly ITryOnJobQueue _queue;
    private readonly ITryOnProvider _provider;
    private readonly IClock _clock;
    private readonly TimeSpan _outputRetention = TimeSpan.FromDays(30);

    public TryOnService(
        IOutfitRepository outfits,
        ITryOnJobRepository jobs,
        ITryOnJobQueue queue,
        ITryOnProvider provider,
        IClock clock)
    {
        _outfits = outfits;
        _jobs = jobs;
        _queue = queue;
        _provider = provider;
        _clock = clock;
    }

    public async Task<TryOnJob> StartAsync(
        string userId,
        Guid outfitId,
        string bodyReferencePhotoUrl,
        bool consentAccepted,
        bool sequentialFlowEnabled = false,
        CancellationToken cancellationToken = default)
    {
        if (!consentAccepted)
        {
            throw new InvalidOperationException("Explicit consent is required before sending photos to an AI provider.");
        }

        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var normalizedBodyPhotoUrl = InputGuard.RequireText(bodyReferencePhotoUrl, "Body reference photo URL");
        var outfit = _outfits.GetOutfitByUser(normalizedUserId, outfitId)
            ?? throw new InvalidOperationException("Outfit was not found.");

        var now = _clock.UtcNow;
        var started = new TryOnJob(
            Guid.NewGuid(),
            normalizedUserId,
            outfitId,
            normalizedBodyPhotoUrl,
            sequentialFlowEnabled,
            TryOnStatus.Queued,
            null,
            null,
            null,
            now,
            now)
        {
            ConsentAcceptedAt = now,
            ProviderName = _provider.Name,
            RetentionUntil = now.Add(_outputRetention),
            IsDeleted = false
        };

        _jobs.AddTryOnJob(started);
        await _queue.EnqueueAsync(started.Id, cancellationToken);
        return started;
    }

    public TryOnJob Start(string userId, Guid outfitId, string bodyReferencePhotoUrl, bool consentAccepted, bool sequentialFlowEnabled = false)
    {
        return StartAsync(userId, outfitId, bodyReferencePhotoUrl, consentAccepted, sequentialFlowEnabled)
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
            var generation = _provider.Generate(queued.UserId, outfit, queued.BodyReferencePhotoUrl, new TryOnOptions(queued.SequentialFlowEnabled));
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
