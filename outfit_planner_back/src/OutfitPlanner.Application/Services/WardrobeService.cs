using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class WardrobeService
{
    private const int MaxListLimit = 100;
    private const string DefaultLaundryStatus = "clean";

    private readonly IBodyReferencePhotoRepository _bodyPhotos;
    private readonly IGarmentRepository _garments;
    private readonly IClock _clock;
    private readonly IStoredPhotoDeletion? _photoDeletion;
    private readonly IGarmentImageRotator? _imageRotator;
    private readonly IBackgroundRemovalJobQueue? _removalQueue;
    private readonly IBackgroundRemovalJobRepository? _removalJobs;

    public WardrobeService(IBodyReferencePhotoRepository bodyPhotos, IGarmentRepository garments, IClock clock, IStoredPhotoDeletion? photoDeletion = null, IGarmentImageRotator? imageRotator = null, IBackgroundRemovalJobQueue? removalQueue = null, IBackgroundRemovalJobRepository? removalJobs = null)
    {
        _bodyPhotos = bodyPhotos;
        _garments = garments;
        _clock = clock;
        _photoDeletion = photoDeletion;
        _imageRotator = imageRotator;
        _removalQueue = removalQueue;
        _removalJobs = removalJobs;
    }

    public BodyReferencePhoto CreateBodyReferencePhoto(string userId, string imageUrl)
    {
        var photo = new BodyReferencePhoto(Guid.NewGuid(), InputGuard.NormalizeUserId(userId), InputGuard.RequireText(imageUrl, "Body reference photo URL"), _clock.UtcNow);
        _bodyPhotos.AddBodyReferencePhoto(photo);
        return photo;
    }

    public IReadOnlyList<BodyReferencePhoto> ListBodyReferencePhotos(string userId)
    {
        return _bodyPhotos.ListBodyReferencePhotosByUser(InputGuard.NormalizeUserId(userId));
    }

    public bool DeleteBodyReferencePhoto(string userId, Guid photoId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var photo = _bodyPhotos.GetBodyReferencePhotoByUser(normalizedUserId, photoId);
        if (photo is null || !_bodyPhotos.DeleteBodyReferencePhotoByUser(normalizedUserId, photoId))
        {
            return false;
        }

        _photoDeletion?.DeleteBodyReferencePhoto(photo.ImageUrl);
        return true;
    }

    public GarmentItem CreateGarment(CreateGarmentCommand command)
    {
        var userId = InputGuard.NormalizeUserId(command.UserId);
        var imageUrl = InputGuard.RequireText(command.ImageUrl, "Garment image URL");
        var thumbnailUrl = string.IsNullOrWhiteSpace(command.ThumbnailUrl) ? imageUrl : command.ThumbnailUrl.Trim();
        var rotationDegrees = 0d;
        var (cutoutWidthPx, cutoutHeightPx) = NormalizeCutoutMeasurement(command.CutoutWidthPx, command.CutoutHeightPx);

        // When background removal is deferred to the async worker, the stored image is the ORIGINAL
        // (no cutout to straighten yet), so skip create-time auto-straighten — the worker does it
        // once the cutout exists.
        var removalPending = command.BackgroundRemovalPending && _removalQueue is not null && _removalJobs is not null;

        // Auto-straighten clothing categories from their silhouette; accessories/shoes/bags/hats
        // are often angled on purpose and are left untouched.
        if (!removalPending && _imageRotator is not null && ShouldAutoStraighten(command.Category))
        {
            var angle = NormalizeRotation(_imageRotator.ComputeGarmentAutoStraightenAngle(imageUrl));
            if (Math.Abs(angle) >= 0.5d)
            {
                var rotated = _imageRotator.RotateGarment(imageUrl, angle);
                imageUrl = rotated.ImageUrl;
                thumbnailUrl = rotated.ThumbnailUrl;
                rotationDegrees = angle;
                if (rotated.CutoutMeasurement is { } straightened)
                {
                    (cutoutWidthPx, cutoutHeightPx) = (straightened.WidthPx, straightened.HeightPx);
                }
            }
        }

        var garment = new GarmentItem(
            Guid.NewGuid(),
            userId,
            InputGuard.RequireText(command.Name, "Garment name"),
            command.Category,
            GarmentRules.GetBodyZone(command.Category),
            imageUrl,
            thumbnailUrl,
            NormalizeTags(command.Tags),
            NormalizeToken(command.PrimaryColor),
            NormalizeTokens(command.SecondaryColors ?? Array.Empty<string>()),
            NormalizeOptionalText(command.Material),
            NormalizeOptionalText(command.Brand),
            NormalizeOptionalText(command.Size),
            NormalizeTokens(command.Season ?? Array.Empty<string>()),
            command.WeatherMinTemp,
            command.WeatherMaxTemp,
            NormalizeTokens(command.Occasion ?? Array.Empty<string>()),
            ValidateScore(command.FormalityScore, "Formality score"),
            ValidateScore(command.WarmthScore, "Warmth score"),
            ValidateScore(command.ComfortScore, "Comfort score"),
            command.IsFavorite,
            command.IsArchived,
            command.LastWornAt,
            NormalizeLaundryStatus(command.LaundryStatus),
            _clock.UtcNow,
            rotationDegrees,
            NormalizeOptionalText(command.PerceptualHash),
            removalPending ? BackgroundRemovalStatus.Pending : BackgroundRemovalStatus.Succeeded,
            CutoutWidthPx: cutoutWidthPx,
            CutoutHeightPx: cutoutHeightPx);

        ValidateWeatherRange(garment.WeatherMinTemp, garment.WeatherMaxTemp);
        _garments.AddGarment(garment);

        if (removalPending)
        {
            var now = _clock.UtcNow;
            var job = new BackgroundRemovalJob(Guid.NewGuid(), userId, imageUrl, imageUrl, BackgroundRemovalStatus.Pending, now, now)
            {
                GarmentId = garment.Id,
                GarmentCategory = garment.Category
            };
            _removalJobs!.AddBackgroundRemovalJob(job);
            _removalQueue!.EnqueueAsync(job.Id).AsTask().GetAwaiter().GetResult();
        }

        return garment;
    }

    public IReadOnlyList<GarmentItem> ListGarments(string userId)
    {
        return ListGarments(userId, new GarmentQuery());
    }

    public IReadOnlyList<GarmentItem> ListGarments(string userId, GarmentQuery query)
    {
        return _garments.ListGarmentsByUser(InputGuard.NormalizeUserId(userId), NormalizeQuery(query));
    }

    public GarmentItem? GetGarment(string userId, Guid garmentId)
    {
        return _garments.GetGarmentByUser(InputGuard.NormalizeUserId(userId), garmentId);
    }

    public GarmentItem? UpdateGarment(string userId, Guid garmentId, UpdateGarmentCommand command)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var existing = _garments.GetGarmentByUser(normalizedUserId, garmentId);
        if (existing is null)
        {
            return null;
        }

        var category = command.Category ?? existing.Category;
        var updated = existing with
        {
            Name = command.Name is null ? existing.Name : InputGuard.RequireText(command.Name, "Garment name"),
            Category = category,
            BodyZone = GarmentRules.GetBodyZone(category),
            Tags = command.Tags is null ? existing.Tags : NormalizeTags(command.Tags),
            PrimaryColor = command.PrimaryColor is null ? existing.PrimaryColor : NormalizeToken(command.PrimaryColor),
            SecondaryColors = command.SecondaryColors is null ? existing.SecondaryColors : NormalizeTokens(command.SecondaryColors),
            Material = command.Material is null ? existing.Material : NormalizeOptionalText(command.Material),
            Brand = command.Brand is null ? existing.Brand : NormalizeOptionalText(command.Brand),
            Size = command.Size is null ? existing.Size : NormalizeOptionalText(command.Size),
            Season = command.Season is null ? existing.Season : NormalizeTokens(command.Season),
            WeatherMinTemp = command.WeatherMinTemp ?? existing.WeatherMinTemp,
            WeatherMaxTemp = command.WeatherMaxTemp ?? existing.WeatherMaxTemp,
            Occasion = command.Occasion is null ? existing.Occasion : NormalizeTokens(command.Occasion),
            FormalityScore = command.FormalityScore is null ? existing.FormalityScore : ValidateScore(command.FormalityScore, "Formality score"),
            WarmthScore = command.WarmthScore is null ? existing.WarmthScore : ValidateScore(command.WarmthScore, "Warmth score"),
            ComfortScore = command.ComfortScore is null ? existing.ComfortScore : ValidateScore(command.ComfortScore, "Comfort score"),
            IsFavorite = command.IsFavorite ?? existing.IsFavorite,
            IsArchived = command.IsArchived ?? existing.IsArchived,
            LastWornAt = command.LastWornAt ?? existing.LastWornAt,
            LaundryStatus = command.LaundryStatus is null ? existing.LaundryStatus : NormalizeLaundryStatus(command.LaundryStatus)
        };

        ValidateWeatherRange(updated.WeatherMinTemp, updated.WeatherMaxTemp);

        // Manual rotate: re-render the displayed cutout/thumbnail/mask from the immutable base
        // at the requested absolute angle (available for every category), and persist the angle.
        if (command.RotationDegrees is { } requestedDegrees && _imageRotator is not null)
        {
            var normalized = NormalizeRotation(requestedDegrees);
            if (Math.Abs(normalized - existing.RotationDegrees) >= 0.5d)
            {
                var rotated = _imageRotator.RotateGarment(existing.ImageUrl, normalized);
                updated = updated with
                {
                    ImageUrl = rotated.ImageUrl,
                    ThumbnailUrl = rotated.ThumbnailUrl,
                    RotationDegrees = normalized,
                    // The re-rendered cutout has a new bounding box, so refresh the measurement
                    // (keep the previous one only if the render could not measure).
                    CutoutWidthPx = rotated.CutoutMeasurement?.WidthPx ?? existing.CutoutWidthPx,
                    CutoutHeightPx = rotated.CutoutMeasurement?.HeightPx ?? existing.CutoutHeightPx
                };
            }
            else
            {
                updated = updated with { RotationDegrees = normalized };
            }
        }

        _garments.UpdateGarment(updated);
        return updated;
    }

    public bool DeleteGarment(string userId, Guid garmentId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var garment = _garments.GetGarmentByUser(normalizedUserId, garmentId);
        if (garment is null || !_garments.DeleteGarmentByUser(normalizedUserId, garmentId))
        {
            return false;
        }

        _photoDeletion?.DeleteGarmentPhoto(garment.ImageUrl);
        if (!string.Equals(garment.ImageUrl, garment.ThumbnailUrl, StringComparison.OrdinalIgnoreCase))
        {
            _photoDeletion?.DeleteGarmentPhoto(garment.ThumbnailUrl);
        }

        return true;
    }

    // Deletes every stored garment and body-reference object (all variants) for a user. Used by
    // account deletion so sensitive binaries are erased before the database rows that hold their
    // object keys are cascade-removed. Best-effort; returns the number of objects removed.
    public int PurgeUserStoredPhotos(string userId)
    {
        if (_photoDeletion is null)
        {
            return 0;
        }

        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var deleted = 0;

        foreach (var garment in _garments.ListGarmentsByUser(normalizedUserId, new GarmentQuery()))
        {
            if (_photoDeletion.DeleteGarmentPhoto(garment.ImageUrl))
            {
                deleted++;
            }

            if (!string.Equals(garment.ImageUrl, garment.ThumbnailUrl, StringComparison.OrdinalIgnoreCase))
            {
                _photoDeletion.DeleteGarmentPhoto(garment.ThumbnailUrl);
            }
        }

        foreach (var photo in _bodyPhotos.ListBodyReferencePhotosByUser(normalizedUserId))
        {
            if (_photoDeletion.DeleteBodyReferencePhoto(photo.ImageUrl))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static GarmentQuery NormalizeQuery(GarmentQuery query)
    {
        return query with
        {
            Color = NormalizeToken(query.Color),
            Season = NormalizeToken(query.Season),
            Search = NormalizeOptionalText(query.Search),
            Sort = NormalizeToken(query.Sort),
            Offset = query.Offset is null ? null : Math.Max(0, query.Offset.Value),
            Limit = query.Limit is null ? null : Math.Clamp(query.Limit.Value, 1, MaxListLimit),
            Occasion = NormalizeToken(query.Occasion),
            Brand = NormalizeOptionalText(query.Brand),
            Material = NormalizeOptionalText(query.Material)
        };
    }

    private static IReadOnlyList<string> NormalizeTokens(IReadOnlyList<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static string? NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ValidateScore(int? score, string label)
    {
        if (score is < 1 or > 5)
        {
            throw new ValidationException($"{label} must be between 1 and 5.");
        }

        return score;
    }

    private static void ValidateWeatherRange(int? minTemp, int? maxTemp)
    {
        if (minTemp is not null && maxTemp is not null && maxTemp < minTemp)
        {
            throw new ValidationException("Weather max temperature must be greater than or equal to weather min temperature.");
        }
    }

    // The measurement is meaningful only as a complete positive pair; anything else (a lone
    // dimension, zero, negative garbage) degrades to "not measured".
    private static (int? WidthPx, int? HeightPx) NormalizeCutoutMeasurement(int? widthPx, int? heightPx)
    {
        return widthPx is > 0 && heightPx is > 0 ? (widthPx, heightPx) : (null, null);
    }

    private static bool ShouldAutoStraighten(GarmentCategory category)
    {
        return category is GarmentCategory.Top or GarmentCategory.Bottom or GarmentCategory.Dress or GarmentCategory.Outerwear;
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

    private static string NormalizeLaundryStatus(string? status)
    {
        var normalized = NormalizeToken(status) ?? DefaultLaundryStatus;
        if (normalized is not ("clean" or "worn" or "washing"))
        {
            throw new ValidationException("Laundry status must be clean, worn, or washing.");
        }

        return normalized;
    }
}
