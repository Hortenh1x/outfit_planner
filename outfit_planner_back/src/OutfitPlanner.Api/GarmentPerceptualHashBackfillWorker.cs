using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Api;

// One-time, best-effort backfill: computes the pre-background-removal perceptual hash for garments
// created before hashing was wired up, so duplicate-upload detection also covers older items.
// Garments whose original image can no longer be read are left un-hashed and simply skipped.
public sealed class GarmentPerceptualHashBackfillWorker : BackgroundService
{
    private const int BatchSize = 200;

    private readonly IGarmentRepository _garments;
    private readonly IGarmentOriginalImageReader _originalImages;
    private readonly IImageProcessor _images;
    private readonly ILogger<GarmentPerceptualHashBackfillWorker> _logger;

    public GarmentPerceptualHashBackfillWorker(
        IGarmentRepository garments,
        IGarmentOriginalImageReader originalImages,
        IImageProcessor images,
        ILogger<GarmentPerceptualHashBackfillWorker> logger)
    {
        _garments = garments;
        _originalImages = originalImages;
        _images = images;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let startup (migrations, storage wiring) settle before touching the database.
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            var updated = 0;
            var skipped = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                var missing = _garments.ListGarmentsMissingPerceptualHash(BatchSize);
                if (missing.Count == 0)
                {
                    break;
                }

                var progressed = false;
                foreach (var garment in missing)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var bytes = _originalImages.ReadGarmentOriginalImageBytes(garment.ImageUrl);
                        var hash = bytes is null ? null : _images.ComputePerceptualHash(bytes);
                        if (string.IsNullOrEmpty(hash))
                        {
                            skipped++;
                            continue;
                        }

                        _garments.UpdateGarment(garment with { PerceptualHash = hash });
                        updated++;
                        progressed = true;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        _logger.LogWarning(ex, "Could not backfill perceptual hash for garment {GarmentId}.", garment.Id);
                    }
                }

                // A whole batch with no successful update means the rest are un-hashable
                // (missing originals); stop rather than re-selecting the same rows forever.
                if (!progressed)
                {
                    break;
                }
            }

            if (updated > 0 || skipped > 0)
            {
                _logger.LogInformation(
                    "Garment perceptual-hash backfill complete: {Updated} updated, {Skipped} skipped.",
                    updated,
                    skipped);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Garment perceptual-hash backfill failed.");
        }
    }
}
