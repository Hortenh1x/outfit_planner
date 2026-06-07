using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Application.Services;

public sealed class PhotoUploadService
{
    public const long MaxPhotoBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IPhotoStorage _storage;

    public PhotoUploadService(IPhotoStorage storage)
    {
        _storage = storage;
    }

    public StoredPhoto UploadGarmentPhoto(IncomingPhoto photo)
    {
        ValidatePhoto(photo);
        return _storage.SaveGarmentPhoto(photo);
    }

    public StoredPhoto UploadBodyReferencePhoto(IncomingPhoto photo)
    {
        ValidatePhoto(photo);
        return _storage.SaveBodyReferencePhoto(photo);
    }

    private static void ValidatePhoto(IncomingPhoto photo)
    {
        if (photo.Length <= 0)
        {
            throw new InvalidOperationException("Photo file is required.");
        }

        if (photo.Length > MaxPhotoBytes)
        {
            throw new InvalidOperationException("Photo file must be 50 MB or smaller.");
        }

        if (!AllowedContentTypes.Contains(photo.ContentType))
        {
            throw new InvalidOperationException("Upload a JPG, PNG, or WebP image.");
        }
    }
}
