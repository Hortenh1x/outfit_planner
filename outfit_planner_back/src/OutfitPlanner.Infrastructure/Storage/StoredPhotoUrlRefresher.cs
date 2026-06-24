using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class StoredPhotoUrlRefresher : IStoredPhotoUrlRefresher
{
    private static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromMinutes(15);
    private const string LocalSignedRoutePrefix = "/api/storage/signed/";

    private readonly IObjectStorage _objects;

    public StoredPhotoUrlRefresher(IObjectStorage objects)
    {
        _objects = objects;
    }

    public string RefreshGarmentImageUrl(string photoUrl)
    {
        return RefreshLocalSignedUrl(photoUrl, "garments", StoredImageVariant.ProcessedCutout);
    }

    public string RefreshGarmentThumbnailUrl(string photoUrl)
    {
        return RefreshLocalSignedUrl(photoUrl, "garments", StoredImageVariant.Thumbnail);
    }

    public string RefreshBodyReferencePhotoUrl(string photoUrl)
    {
        return RefreshLocalSignedUrl(photoUrl, "body-reference-photos", StoredImageVariant.Original);
    }

    private string RefreshLocalSignedUrl(string photoUrl, string collection, StoredImageVariant preferredVariant)
    {
        var objectKey = TryReadLocalSignedObjectKey(photoUrl);
        if (objectKey is null)
        {
            return photoUrl;
        }

        var refreshedObjectKey = PreferredVariantObjectKey(objectKey, collection, preferredVariant)
            ?? (ObjectExists(objectKey) ? objectKey : null);
        return refreshedObjectKey is null
            ? photoUrl
            : _objects.CreateSignedReadUrl(refreshedObjectKey, SignedUrlLifetime);
    }

    private string? PreferredVariantObjectKey(string objectKey, string collection, StoredImageVariant preferredVariant)
    {
        var normalizedCollection = collection.Trim('/');
        if (!objectKey.StartsWith(normalizedCollection + "/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = objectKey.Split('/', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        var candidate = $"{normalizedCollection}/{VariantFolder(preferredVariant)}/{parts[2]}";
        return ObjectExists(candidate) ? candidate : null;
    }

    private bool ObjectExists(string objectKey)
    {
        try
        {
            return _objects.GetObject(objectKey) is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string? TryReadLocalSignedObjectKey(string photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        var path = Uri.TryCreate(photoUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : photoUrl.Split('?', 2)[0];
        if (!path.StartsWith(LocalSignedRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var encodedKey = path[LocalSignedRoutePrefix.Length..].Trim('/');
        return string.IsNullOrWhiteSpace(encodedKey)
            ? null
            : Uri.UnescapeDataString(encodedKey);
    }

    private static string VariantFolder(StoredImageVariant variant)
    {
        return variant switch
        {
            StoredImageVariant.Original => "original",
            StoredImageVariant.Thumbnail => "thumbnail",
            StoredImageVariant.ProcessedCutout => "processed-cutout",
            StoredImageVariant.TryOnOutput => "try-on-output",
            StoredImageVariant.PrivatePreview => "private-preview",
            StoredImageVariant.SegmentationMask => "segmentation-mask",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unsupported image variant.")
        };
    }
}
