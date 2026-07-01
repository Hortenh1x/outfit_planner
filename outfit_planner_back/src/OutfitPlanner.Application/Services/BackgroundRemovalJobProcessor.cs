using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

/// <summary>
/// Processes one queued background-removal job: runs rembg on the garment's stored original,
/// applies clothing-only auto-straighten, overwrites the garment's cutout in place, and moves the
/// garment (and job) to Succeeded/Failed. Mirrors <c>TryOnService.ProcessQueuedJobAsync</c>.
/// </summary>
public sealed class BackgroundRemovalJobProcessor : IBackgroundRemovalJobProcessor
{
    private static readonly HashSet<GarmentCategory> AutoStraightenCategories = new()
    {
        GarmentCategory.Top,
        GarmentCategory.Bottom,
        GarmentCategory.Dress,
        GarmentCategory.Outerwear
    };

    private readonly IBackgroundRemovalJobRepository _jobs;
    private readonly IGarmentRepository _garments;
    private readonly IGarmentBackgroundRemover _remover;
    private readonly IClock _clock;
    private readonly IGarmentImageRotator? _rotator;

    public BackgroundRemovalJobProcessor(
        IBackgroundRemovalJobRepository jobs,
        IGarmentRepository garments,
        IGarmentBackgroundRemover remover,
        IClock clock,
        IGarmentImageRotator? rotator = null)
    {
        _jobs = jobs;
        _garments = garments;
        _remover = remover;
        _clock = clock;
        _rotator = rotator;
    }

    public Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = _jobs.GetBackgroundRemovalJobById(jobId);
        if (job is null)
        {
            return Task.CompletedTask;
        }

        var garment = job.GarmentId is { } garmentId
            ? _garments.GetGarmentByUser(job.UserId, garmentId)
            : null;

        try
        {
            _jobs.UpdateBackgroundRemovalJob(job with { Status = BackgroundRemovalStatus.Processing, UpdatedAt = _clock.UtcNow });
            if (garment is not null)
            {
                _garments.UpdateGarment(garment with
                {
                    BackgroundRemovalStatus = BackgroundRemovalStatus.Processing,
                    BackgroundRemovalError = null
                });
            }

            var sourceUrl = garment?.ImageUrl ?? job.OriginalUrl;
            var removal = _remover.RemoveGarmentBackground(sourceUrl);

            var imageUrl = removal.CutoutUrl;
            var thumbnailUrl = removal.ThumbnailUrl;
            var rotationDegrees = 0d;

            // Auto-straighten clothing categories now that the base cutout exists (needs the category,
            // which only the garment knows).
            if (garment is not null && _rotator is not null && AutoStraightenCategories.Contains(garment.Category))
            {
                var angle = NormalizeRotation(_rotator.ComputeGarmentAutoStraightenAngle(imageUrl));
                if (Math.Abs(angle) >= 0.5d)
                {
                    var rotated = _rotator.RotateGarment(imageUrl, angle);
                    imageUrl = rotated.ImageUrl;
                    thumbnailUrl = rotated.ThumbnailUrl;
                    rotationDegrees = angle;
                }
            }

            if (garment is not null)
            {
                _garments.UpdateGarment(garment with
                {
                    ImageUrl = imageUrl,
                    ThumbnailUrl = thumbnailUrl,
                    RotationDegrees = rotationDegrees,
                    PerceptualHash = removal.PerceptualHash ?? garment.PerceptualHash,
                    BackgroundRemovalStatus = BackgroundRemovalStatus.Succeeded,
                    BackgroundRemovalError = null
                });
            }

            _jobs.UpdateBackgroundRemovalJob(job with
            {
                Status = BackgroundRemovalStatus.Succeeded,
                CutoutUrl = imageUrl,
                ThumbnailUrl = thumbnailUrl,
                UpdatedAt = _clock.UtcNow
            });
        }
        catch (Exception ex)
        {
            _jobs.UpdateBackgroundRemovalJob(job with { Status = BackgroundRemovalStatus.Failed, Error = ex.Message, UpdatedAt = _clock.UtcNow });
            if (garment is not null)
            {
                var current = _garments.GetGarmentByUser(job.UserId, garment.Id);
                if (current is not null)
                {
                    _garments.UpdateGarment(current with
                    {
                        BackgroundRemovalStatus = BackgroundRemovalStatus.Failed,
                        BackgroundRemovalError = ex.Message
                    });
                }
            }
        }

        return Task.CompletedTask;
    }

    private static double NormalizeRotation(double degrees)
    {
        var wrapped = degrees % 360d;
        if (wrapped > 180d)
        {
            wrapped -= 360d;
        }
        else if (wrapped <= -180d)
        {
            wrapped += 360d;
        }

        return wrapped;
    }
}
