namespace OutfitPlanner.Application.Abstractions;

public sealed record IncomingPhoto(string FileName, string ContentType, long Length, Stream Content);

public sealed record StoredPhoto(string FileName, string ContentType, long Length, string Url)
{
    public string? OriginalUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? ProcessedCutoutUrl { get; init; }
    public string? SegmentationMaskUrl { get; init; }
    public string? ObjectKey { get; init; }
    public string? ThumbnailObjectKey { get; init; }
    public string? ProcessedCutoutObjectKey { get; init; }
    public string? PrivatePreviewObjectKey { get; init; }
    public string? PerceptualHash { get; init; }
    public string? BaseCutoutUrl { get; init; }
    public string? BaseCutoutObjectKey { get; init; }
}

public sealed record StoredPhotoFile(string FullPath, string ContentType);

public enum StoredImageVariant
{
    Original,
    Thumbnail,
    ProcessedCutout,
    TryOnOutput,
    PrivatePreview,
    SegmentationMask,
    BaseCutout
}

public sealed record ProcessedImage(
    StoredImageVariant Variant,
    string ContentType,
    string Extension,
    byte[] Bytes);

public sealed record ProcessedPhotoSet(
    string FileName,
    string ContentType,
    long Length,
    string? PerceptualHash,
    IReadOnlyList<ProcessedImage> Images);

public sealed record ObjectStoragePutRequest(
    string ObjectKey,
    string ContentType,
    Stream Content,
    bool Private,
    DateTimeOffset? RetentionUntil = null);

public sealed record StoredObject(
    string ObjectKey,
    string ContentType,
    long Length,
    bool Private);

public sealed record StoredObjectFile(string FullPath, string ContentType);

public interface IImageProcessor
{
    ProcessedPhotoSet ProcessGarmentPhoto(IncomingPhoto photo);

    // Original + thumbnail only (no rembg) — the fast path for the non-blocking add flow.
    ProcessedPhotoSet ProcessGarmentOriginal(IncomingPhoto photo);

    ProcessedPhotoSet ProcessBodyReferencePhoto(IncomingPhoto photo);

    ProcessedPhotoSet ProcessAvatarPhoto(IncomingPhoto photo);

    double ComputeGarmentDeskewAngle(byte[] cutoutPngBytes);

    GarmentRotationRender RenderRotatedGarment(byte[] baseCutoutPngBytes, double degrees);

    // Average hash of an image's content, used to detect duplicate garment uploads by the
    // original photo (before background removal). Returns null for empty/invalid input.
    string? ComputePerceptualHash(byte[] imageBytes);
}

public sealed record GarmentRotationRender(
    byte[] CutoutPng,
    byte[] ThumbnailPng,
    byte[] SegmentationMaskPng,
    string PerceptualHash);

public interface IGarmentImageRotator
{
    // Conservative auto-straighten angle for the garment's base cutout (0 when none/uncertain).
    double ComputeGarmentAutoStraightenAngle(string garmentImageUrl);

    // Renders the immutable base cutout at the absolute angle, overwrites the displayed
    // cutout/thumbnail/mask in place, and returns freshly signed display URLs.
    GarmentRotationOutcome RotateGarment(string garmentImageUrl, double degrees);
}

public sealed record GarmentRotationOutcome(string ImageUrl, string ThumbnailUrl, string? PerceptualHash);

public interface IGarmentOriginalImageReader
{
    // Reads the stored ORIGINAL (pre-background-removal) image bytes for a garment given its
    // display image URL, or null when the original is unavailable (e.g. a legacy garment).
    byte[]? ReadGarmentOriginalImageBytes(string garmentImageUrl);
}

public interface IGarmentBackgroundRemover
{
    // Runs background removal on a garment's stored ORIGINAL, overwrites the displayed
    // cutout/thumbnail/base-cutout/mask variants in place, and returns fresh cutout URLs + hash.
    GarmentRemovalOutcome RemoveGarmentBackground(string garmentImageUrl);
}

public sealed record GarmentRemovalOutcome(string CutoutUrl, string ThumbnailUrl, string? PerceptualHash);

public interface IObjectStorage
{
    StoredObject PutObject(ObjectStoragePutRequest request);

    StoredObjectFile? GetObject(string objectKey);

    // Reads an object's bytes through the storage abstraction (not a local file path), so
    // server-side reads (e.g. garment rotation/auto-straighten) work under S3/MinIO as well as
    // local storage. Returns null when the object does not exist. The caller owns the stream.
    Stream? OpenReadObject(string objectKey);

    bool DeleteObject(string objectKey);

    int DeletePrefix(string prefix);

    string CreateSignedReadUrl(string objectKey, TimeSpan lifetime);
}

public interface IPhotoStorage
{
    StoredPhoto SaveGarmentPhoto(IncomingPhoto photo);

    StoredPhoto SaveGarmentOriginal(IncomingPhoto photo);

    StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo);

    StoredPhoto SaveAvatarPhoto(IncomingPhoto photo);
}

public interface IStoredPhotoReader
{
    StoredPhotoFile? GetGarmentPhoto(string fileName);

    StoredPhotoFile? GetBodyReferencePhoto(string fileName);
}

public interface IStoredPhotoDeletion
{
    bool DeleteGarmentPhoto(string photoUrl);

    bool DeleteBodyReferencePhoto(string photoUrl);

    bool DeleteAvatarPhoto(string photoUrl);
}

public interface ITryOnOutputStorage
{
    Task<string> StoreAsync(Guid jobId, string sourceImageUrl, DateTimeOffset retentionUntil, CancellationToken cancellationToken = default);

    bool DeleteOutput(string outputImageUrl);
}

public interface IStoredPhotoUrlRefresher
{
    string RefreshGarmentImageUrl(string photoUrl);

    string RefreshGarmentThumbnailUrl(string photoUrl);

    string RefreshBodyReferencePhotoUrl(string photoUrl);

    string RefreshAvatarUrl(string photoUrl);
}
