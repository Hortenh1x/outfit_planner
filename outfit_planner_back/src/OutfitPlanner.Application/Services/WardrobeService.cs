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

    public WardrobeService(IBodyReferencePhotoRepository bodyPhotos, IGarmentRepository garments, IClock clock, IStoredPhotoDeletion? photoDeletion = null)
    {
        _bodyPhotos = bodyPhotos;
        _garments = garments;
        _clock = clock;
        _photoDeletion = photoDeletion;
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
        var garment = new GarmentItem(
            Guid.NewGuid(),
            userId,
            InputGuard.RequireText(command.Name, "Garment name"),
            command.Category,
            GarmentRules.GetBodyZone(command.Category),
            imageUrl,
            string.IsNullOrWhiteSpace(command.ThumbnailUrl) ? imageUrl : command.ThumbnailUrl.Trim(),
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
            _clock.UtcNow);

        ValidateWeatherRange(garment.WeatherMinTemp, garment.WeatherMaxTemp);
        _garments.AddGarment(garment);
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
            throw new InvalidOperationException($"{label} must be between 1 and 5.");
        }

        return score;
    }

    private static void ValidateWeatherRange(int? minTemp, int? maxTemp)
    {
        if (minTemp is not null && maxTemp is not null && maxTemp < minTemp)
        {
            throw new InvalidOperationException("Weather max temperature must be greater than or equal to weather min temperature.");
        }
    }

    private static string NormalizeLaundryStatus(string? status)
    {
        var normalized = NormalizeToken(status) ?? DefaultLaundryStatus;
        if (normalized is not ("clean" or "worn" or "washing"))
        {
            throw new InvalidOperationException("Laundry status must be clean, worn, or washing.");
        }

        return normalized;
    }
}
