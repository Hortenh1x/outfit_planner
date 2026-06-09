using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class LocalPhotoStorage : IPhotoStorage, IStoredPhotoReader, IStoredPhotoDeletion
{
    private static readonly TimeSpan DefaultSignedUrlLifetime = TimeSpan.FromMinutes(15);

    private readonly IObjectStorage _objects;
    private readonly IImageProcessor _images;

    public LocalPhotoStorage(string storageRoot)
        : this(new LocalObjectStorage(storageRoot), new ImageProcessor())
    {
    }

    public LocalPhotoStorage(IObjectStorage objects, IImageProcessor images)
    {
        _objects = objects;
        _images = images;
    }

    public StoredPhoto SaveGarmentPhoto(IncomingPhoto photo)
    {
        var processed = _images.ProcessGarmentPhoto(photo);
        return SaveProcessedPhoto("garments", processed);
    }

    public StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo)
    {
        var processed = _images.ProcessBodyReferencePhoto(photo);
        return SaveProcessedPhoto("body-reference-photos", processed);
    }

    public StoredPhotoFile? GetGarmentPhoto(string fileName)
    {
        return GetPhoto("garments", StoredImageVariant.Original, fileName);
    }

    public StoredPhotoFile? GetBodyReferencePhoto(string fileName)
    {
        return GetPhoto("body-reference-photos", StoredImageVariant.Original, fileName);
    }

    public bool DeleteGarmentPhoto(string photoUrl)
    {
        return DeletePhoto("garments", photoUrl);
    }

    public bool DeleteBodyReferencePhoto(string photoUrl)
    {
        return DeletePhoto("body-reference-photos", photoUrl);
    }

    private StoredPhoto SaveProcessedPhoto(string collection, ProcessedPhotoSet processed)
    {
        var storedByVariant = new Dictionary<StoredImageVariant, StoredObject>();
        foreach (var image in processed.Images)
        {
            var objectKey = ObjectKey(collection, image.Variant, processed.FileName);
            using var stream = new MemoryStream(image.Bytes, writable: false);
            storedByVariant[image.Variant] = _objects.PutObject(new ObjectStoragePutRequest(
                objectKey,
                image.ContentType,
                stream,
                Private: true));
        }

        var original = storedByVariant[StoredImageVariant.Original];
        return new StoredPhoto(
            processed.FileName,
            processed.ContentType,
            processed.Length,
            _objects.CreateSignedReadUrl(original.ObjectKey, DefaultSignedUrlLifetime))
        {
            ObjectKey = original.ObjectKey,
            ThumbnailObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.Thumbnail)?.ObjectKey,
            ProcessedCutoutObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.ProcessedCutout)?.ObjectKey,
            PrivatePreviewObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.PrivatePreview)?.ObjectKey,
            PerceptualHash = processed.PerceptualHash
        };
    }

    private StoredPhotoFile? GetPhoto(string collection, StoredImageVariant variant, string fileName)
    {
        if (!IsSafeStoredFileName(fileName))
        {
            return null;
        }

        var objectFile = _objects.GetObject(ObjectKey(collection, variant, fileName));
        return objectFile is null ? null : new StoredPhotoFile(objectFile.FullPath, objectFile.ContentType);
    }

    private bool DeletePhoto(string collection, string photoUrl)
    {
        var fileName = FileNameFromPhotoUrl(photoUrl);
        if (fileName is null)
        {
            return false;
        }

        var deleted = 0;
        foreach (var variant in Enum.GetValues<StoredImageVariant>())
        {
            if (_objects.DeleteObject(ObjectKey(collection, variant, fileName)))
            {
                deleted++;
            }
        }

        return deleted > 0;
    }

    private static string ObjectKey(string collection, StoredImageVariant variant, string fileName)
    {
        return $"{collection}/{VariantFolder(variant)}/{fileName}";
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

    private static string? FileNameFromPhotoUrl(string photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        var path = Uri.TryCreate(photoUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : photoUrl.Split('?', 2)[0];

        var fileName = Path.GetFileName(path);
        return IsSafeStoredFileName(fileName) ? fileName : null;
    }

    private static bool IsSafeStoredFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName == Path.GetFileName(fileName)
            && ContentTypeForExtension(Path.GetExtension(fileName)) is not null;
    }

    private static string? ContentTypeForExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
    }
}
