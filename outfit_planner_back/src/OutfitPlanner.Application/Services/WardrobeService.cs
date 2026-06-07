using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class WardrobeService
{
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
            _clock.UtcNow);

        _garments.AddGarment(garment);
        return garment;
    }

    public IReadOnlyList<GarmentItem> ListGarments(string userId)
    {
        return _garments.ListGarmentsByUser(InputGuard.NormalizeUserId(userId));
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
}
