using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Api;

// One-time, best-effort backfill: measures the alpha bounding box of the stored cutout for
// garments created before cutout measurement was wired up, so relative sizing also covers older
// items. Prefers the processed cutout; falls back to the original (which measures as its full
// frame when it has no transparency). Garments with no readable image are simply skipped.
public sealed class GarmentCutoutMeasurementBackfillWorker : BackgroundService
{
    private const int BatchSize = 200;

    private readonly IGarmentRepository _garments;
    private readonly IGarmentCutoutImageReader _cutoutImages;
    private readonly IGarmentOriginalImageReader _originalImages;
    private readonly IImageProcessor _images;
    private readonly ILogger<GarmentCutoutMeasurementBackfillWorker> _logger;

    public GarmentCutoutMeasurementBackfillWorker(
        IGarmentRepository garments,
        IGarmentCutoutImageReader cutoutImages,
        IGarmentOriginalImageReader originalImages,
        IImageProcessor images,
        ILogger<GarmentCutoutMeasurementBackfillWorker> logger)
    {
        _garments = garments;
        _cutoutImages = cutoutImages;
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
                var missing = _garments.ListGarmentsMissingCutoutMeasurement(BatchSize);
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
                        var bytes = _cutoutImages.ReadGarmentCutoutImageBytes(garment.ImageUrl)
                            ?? _originalImages.ReadGarmentOriginalImageBytes(garment.ImageUrl);
                        var measurement = bytes is null ? null : _images.MeasureGarmentCutout(bytes);
                        if (measurement is null)
                        {
                            skipped++;
                            continue;
                        }

                        // Column-scoped update: the perceptual-hash backfill may be touching the
                        // same row concurrently, and a whole-record rewrite would race with it.
                        _garments.UpdateGarmentCutoutMeasurement(garment.Id, measurement.WidthPx, measurement.HeightPx);
                        updated++;
                        progressed = true;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        _logger.LogWarning(ex, "Could not backfill cutout measurement for garment {GarmentId}.", garment.Id);
                    }
                }

                // A whole batch with no successful update means the rest are unmeasurable
                // (missing images); stop rather than re-selecting the same rows forever.
                if (!progressed)
                {
                    break;
                }
            }

            if (updated > 0 || skipped > 0)
            {
                _logger.LogInformation(
                    "Garment cutout-measurement backfill complete: {Updated} updated, {Skipped} skipped.",
                    updated,
                    skipped);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Garment cutout-measurement backfill failed.");
        }
    }
}
