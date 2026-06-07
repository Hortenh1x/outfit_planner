using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class TryOnService
{
    private readonly IOutfitRepository _outfits;
    private readonly ITryOnJobRepository _jobs;
    private readonly ITryOnProvider _provider;
    private readonly IClock _clock;

    public TryOnService(
        IOutfitRepository outfits,
        ITryOnJobRepository jobs,
        ITryOnProvider provider,
        IClock clock)
    {
        _outfits = outfits;
        _jobs = jobs;
        _provider = provider;
        _clock = clock;
    }

    public TryOnJob Start(string userId, Guid outfitId, string bodyReferencePhotoUrl, bool consentAccepted, bool sequentialFlowEnabled = false)
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
            TryOnStatus.Processing,
            null,
            null,
            null,
            now,
            now);

        _jobs.AddTryOnJob(started);

        try
        {
            var generation = _provider.Generate(normalizedUserId, outfit, normalizedBodyPhotoUrl, new TryOnOptions(sequentialFlowEnabled));
            var completed = started with
            {
                Status = TryOnStatus.Succeeded,
                ProviderJobId = generation.ProviderJobId,
                OutputImageUrl = generation.OutputImageUrl,
                UpdatedAt = _clock.UtcNow
            };

            _jobs.UpdateTryOnJob(completed);
            _outfits.UpdateOutfit(outfit with { PersonPreviewUrl = generation.OutputImageUrl });
            return completed;
        }
        catch (Exception ex)
        {
            var failed = started with
            {
                Status = TryOnStatus.Failed,
                Error = ex.Message,
                UpdatedAt = _clock.UtcNow
            };
            _jobs.UpdateTryOnJob(failed);
            return failed;
        }
    }

    public TryOnJob? GetJob(string userId, Guid jobId)
    {
        return _jobs.GetTryOnJobByUser(InputGuard.NormalizeUserId(userId), jobId);
    }
}
