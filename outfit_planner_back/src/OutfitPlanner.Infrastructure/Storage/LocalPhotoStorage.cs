using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class LocalPhotoStorage : IPhotoStorage, IStoredPhotoReader, IStoredPhotoDeletion, IGarmentImageRotator, IGarmentOriginalImageReader, IGarmentCutoutImageReader, IGarmentBackgroundRemover
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
        return SaveProcessedPhoto("garments", processed, StoredImageVariant.ProcessedCutout);
    }

    public StoredPhoto SaveGarmentOriginal(IncomingPhoto photo)
    {
        var processed = _images.ProcessGarmentOriginal(photo);
        return SaveProcessedPhoto("garments", processed, StoredImageVariant.Original);
    }

    public StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo)
    {
        var processed = _images.ProcessBodyReferencePhoto(photo);
        return SaveProcessedPhoto("body-reference-photos", processed, StoredImageVariant.Original);
    }

    public StoredPhoto SaveAvatarPhoto(IncomingPhoto photo)
    {
        var processed = _images.ProcessAvatarPhoto(photo);
        return SaveProcessedPhoto("avatars", processed, StoredImageVariant.Thumbnail);
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

    public bool DeleteAvatarPhoto(string photoUrl)
    {
        return DeletePhoto("avatars", photoUrl);
    }

    public double ComputeGarmentAutoStraightenAngle(string garmentImageUrl)
    {
        var baseBytes = LoadGarmentBaseCutout(garmentImageUrl);
        return baseBytes is null ? 0d : _images.ComputeGarmentDeskewAngle(baseBytes);
    }

    public GarmentRotationOutcome RotateGarment(string garmentImageUrl, double degrees)
    {
        var fileName = FileNameFromPhotoUrl(garmentImageUrl)
            ?? throw new InvalidOperationException("Cannot resolve the garment image to rotate.");
        var baseBytes = LoadGarmentBaseCutout(garmentImageUrl)
            ?? throw new InvalidOperationException("The garment has no base cutout to rotate from.");

        var rendered = _images.RenderRotatedGarment(baseBytes, degrees);
        var cutout = OverwriteGarmentVariant(StoredImageVariant.ProcessedCutout, fileName, rendered.CutoutPng);
        var thumbnail = OverwriteGarmentVariant(StoredImageVariant.Thumbnail, fileName, rendered.ThumbnailPng);
        OverwriteGarmentVariant(StoredImageVariant.SegmentationMask, fileName, rendered.SegmentationMaskPng);

        return new GarmentRotationOutcome(SignedUrl(cutout), SignedUrl(thumbnail), rendered.PerceptualHash, rendered.CutoutMeasurement);
    }

    public GarmentRemovalOutcome RemoveGarmentBackground(string garmentImageUrl)
    {
        var fileName = FileNameFromPhotoUrl(garmentImageUrl)
            ?? throw new InvalidOperationException("Cannot resolve the garment image for background removal.");
        var originalBytes = ReadGarmentOriginalImageBytes(garmentImageUrl)
            ?? throw new InvalidOperationException("The garment original image is unavailable for background removal.");

        // Reuse the full garment pipeline (rembg + variants) on the stored original, then overwrite
        // the garment's existing cutout/thumbnail/base-cutout/mask objects in place (same fileName).
        var processed = _images.ProcessGarmentPhoto(new IncomingPhoto(
            fileName,
            "image/png",
            originalBytes.LongLength,
            new MemoryStream(originalBytes, writable: false)));

        StoredObject? cutout = null;
        StoredObject? thumbnail = null;
        foreach (var image in processed.Images)
        {
            switch (image.Variant)
            {
                case StoredImageVariant.ProcessedCutout:
                    cutout = OverwriteGarmentVariant(StoredImageVariant.ProcessedCutout, fileName, image.Bytes);
                    break;
                case StoredImageVariant.BaseCutout:
                    OverwriteGarmentVariant(StoredImageVariant.BaseCutout, fileName, image.Bytes);
                    break;
                case StoredImageVariant.Thumbnail:
                    thumbnail = OverwriteGarmentVariant(StoredImageVariant.Thumbnail, fileName, image.Bytes);
                    break;
                case StoredImageVariant.SegmentationMask:
                    OverwriteGarmentVariant(StoredImageVariant.SegmentationMask, fileName, image.Bytes);
                    break;
            }
        }

        if (cutout is null || thumbnail is null)
        {
            throw new InvalidOperationException("Background removal did not produce a cutout.");
        }

        return new GarmentRemovalOutcome(SignedUrl(cutout), SignedUrl(thumbnail), processed.PerceptualHash, processed.CutoutMeasurement);
    }

    public byte[]? ReadGarmentOriginalImageBytes(string garmentImageUrl)
    {
        return ReadGarmentVariantBytes(garmentImageUrl, StoredImageVariant.Original);
    }

    public byte[]? ReadGarmentCutoutImageBytes(string garmentImageUrl)
    {
        return ReadGarmentVariantBytes(garmentImageUrl, StoredImageVariant.ProcessedCutout);
    }

    private byte[]? ReadGarmentVariantBytes(string garmentImageUrl, StoredImageVariant variant)
    {
        var fileName = FileNameFromPhotoUrl(garmentImageUrl);
        if (fileName is null)
        {
            return null;
        }

        using var stream = _objects.OpenReadObject(ObjectKey("garments", variant, fileName));
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private byte[]? LoadGarmentBaseCutout(string garmentImageUrl)
    {
        var fileName = FileNameFromPhotoUrl(garmentImageUrl);
        if (fileName is null)
        {
            return null;
        }

        // Prefer the immutable base; fall back to the current cutout for legacy garments.
        // Read through the storage abstraction (not a local file path) so rotation/auto-straighten
        // work under S3/MinIO as well as local storage.
        using var stream = _objects.OpenReadObject(ObjectKey("garments", StoredImageVariant.BaseCutout, fileName))
            ?? _objects.OpenReadObject(ObjectKey("garments", StoredImageVariant.ProcessedCutout, fileName));
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private StoredObject OverwriteGarmentVariant(StoredImageVariant variant, string fileName, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return _objects.PutObject(new ObjectStoragePutRequest(
            ObjectKey("garments", variant, fileName),
            "image/png",
            stream,
            Private: true));
    }

    private StoredPhoto SaveProcessedPhoto(string collection, ProcessedPhotoSet processed, StoredImageVariant primaryVariant)
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
        var primary = storedByVariant.GetValueOrDefault(primaryVariant) ?? original;
        return new StoredPhoto(
            processed.FileName,
            processed.ContentType,
            processed.Length,
            SignedUrl(primary))
        {
            OriginalUrl = SignedUrl(original),
            ThumbnailUrl = OptionalSignedUrl(storedByVariant.GetValueOrDefault(StoredImageVariant.Thumbnail)),
            ProcessedCutoutUrl = OptionalSignedUrl(storedByVariant.GetValueOrDefault(StoredImageVariant.ProcessedCutout)),
            SegmentationMaskUrl = OptionalSignedUrl(storedByVariant.GetValueOrDefault(StoredImageVariant.SegmentationMask)),
            ObjectKey = original.ObjectKey,
            ThumbnailObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.Thumbnail)?.ObjectKey,
            ProcessedCutoutObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.ProcessedCutout)?.ObjectKey,
            PrivatePreviewObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.PrivatePreview)?.ObjectKey,
            PerceptualHash = processed.PerceptualHash,
            BaseCutoutUrl = OptionalSignedUrl(storedByVariant.GetValueOrDefault(StoredImageVariant.BaseCutout)),
            BaseCutoutObjectKey = storedByVariant.GetValueOrDefault(StoredImageVariant.BaseCutout)?.ObjectKey,
            CutoutMeasurement = processed.CutoutMeasurement
        };
    }

    private string SignedUrl(StoredObject stored)
    {
        return _objects.CreateSignedReadUrl(stored.ObjectKey, DefaultSignedUrlLifetime);
    }

    private string? OptionalSignedUrl(StoredObject? stored)
    {
        return stored is null ? null : _objects.CreateSignedReadUrl(stored.ObjectKey, DefaultSignedUrlLifetime);
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
            StoredImageVariant.BaseCutout => "base-cutout",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unsupported image variant.")
        };
    }

    private static string? FileNameFromPhotoUrl(string photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        // Only treat genuine http(s) URLs as absolute. On .NET/Linux a leading-slash
        // relative signed URL ("/api/storage/signed/...") parses as an absolute file: URI,
        // which folds the "?query" into the path as "%3F..." and corrupts the file name.
        var path = Uri.TryCreate(photoUrl, UriKind.Absolute, out var absoluteUri)
            && absoluteUri.Scheme is "http" or "https"
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
