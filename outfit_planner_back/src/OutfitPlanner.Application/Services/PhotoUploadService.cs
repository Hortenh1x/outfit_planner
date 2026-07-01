using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

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
        return _storage.SaveGarmentPhoto(ValidateAndBufferPhoto(photo));
    }

    // Fast path: stores the original + thumbnail only (no rembg) for the non-blocking add flow.
    public StoredPhoto UploadGarmentOriginal(IncomingPhoto photo)
    {
        return _storage.SaveGarmentOriginal(ValidateAndBufferPhoto(photo));
    }

    public StoredPhoto UploadBodyReferencePhoto(IncomingPhoto photo)
    {
        return _storage.SaveBodyReferencePhoto(ValidateAndBufferPhoto(photo));
    }

    public StoredPhoto UploadAvatarPhoto(IncomingPhoto photo)
    {
        return _storage.SaveAvatarPhoto(ValidateAndBufferPhoto(photo));
    }

    private static IncomingPhoto ValidateAndBufferPhoto(IncomingPhoto photo)
    {
        if (photo.Length <= 0)
        {
            throw new ValidationException("Photo file is required.");
        }

        if (photo.Length > MaxPhotoBytes)
        {
            throw new ValidationException("Photo file must be 50 MB or smaller.");
        }

        if (!AllowedContentTypes.Contains(photo.ContentType))
        {
            throw new ValidationException("Upload a JPG, PNG, or WebP image.");
        }

        using var buffer = new MemoryStream();
        photo.Content.CopyTo(buffer);
        var bytes = buffer.ToArray();
        if (bytes.LongLength != photo.Length)
        {
            throw new ValidationException("Photo upload length did not match the received file.");
        }

        var detectedContentType = DetectContentType(bytes);
        if (detectedContentType is null)
        {
            throw new ValidationException("Upload a valid JPG, PNG, or WebP image.");
        }

        if (!string.Equals(photo.ContentType, detectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Photo content does not match the declared image type.");
        }

        return photo with { Content = new MemoryStream(bytes, writable: false) };
    }

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }
}
