using OutfitPlanner.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class ImageProcessor : IImageProcessor
{
    private const int MaxImageSide = 1600;
    private const int ThumbnailSide = 512;
    private const int PrivatePreviewSide = 96;

    private readonly IGarmentExtractionProvider _garmentExtraction;

    public ImageProcessor()
        : this(new SimpleBackgroundRemovalProvider())
    {
    }

    public ImageProcessor(IBackgroundRemovalProvider backgroundRemoval)
        : this(new SingleGarmentExtractionProvider(backgroundRemoval))
    {
    }

    public ImageProcessor(IGarmentExtractionProvider garmentExtraction)
    {
        _garmentExtraction = garmentExtraction;
    }

    public ProcessedPhotoSet ProcessGarmentPhoto(IncomingPhoto photo)
    {
        var bytes = ReadAllBytes(photo.Content);
        using var image = Image.Load<Rgba32>(bytes);
        NormalizeMetadataAndSize(image, MaxImageSide);

        var extension = ExtensionFor(photo.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var original = Encode(image, photo.ContentType);
        using var cutoutImage = CreateGarmentCutout(photo.FileName, photo.ContentType, original);
        using var thumbnailImage = ResizeClone(cutoutImage, ThumbnailSide);
        var thumbnail = EncodePng(thumbnailImage);
        var cutout = EncodePng(cutoutImage);
        var mask = CreateSegmentationMask(cutoutImage);

        return new ProcessedPhotoSet(
            fileName,
            photo.ContentType,
            original.LongLength,
            AverageHash(image),
            new[]
            {
                new ProcessedImage(StoredImageVariant.Original, photo.ContentType, extension, original),
                new ProcessedImage(StoredImageVariant.Thumbnail, "image/png", ".png", thumbnail),
                new ProcessedImage(StoredImageVariant.ProcessedCutout, "image/png", ".png", cutout),
                new ProcessedImage(StoredImageVariant.BaseCutout, "image/png", ".png", cutout),
                new ProcessedImage(StoredImageVariant.SegmentationMask, "image/png", ".png", mask)
            });
    }

    public ProcessedPhotoSet ProcessBodyReferencePhoto(IncomingPhoto photo)
    {
        var bytes = ReadAllBytes(photo.Content);
        using var image = Image.Load<Rgba32>(bytes);
        NormalizeMetadataAndSize(image, MaxImageSide);

        var extension = ExtensionFor(photo.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var original = Encode(image, photo.ContentType);
        var thumbnail = Encode(ResizeClone(image, ThumbnailSide), photo.ContentType);
        using var previewImage = ResizeClone(image, PrivatePreviewSide);
        previewImage.Mutate(operation => operation.GaussianBlur(8f));
        var privatePreview = Encode(previewImage, photo.ContentType);

        return new ProcessedPhotoSet(
            fileName,
            photo.ContentType,
            original.LongLength,
            AverageHash(image),
            new[]
            {
                new ProcessedImage(StoredImageVariant.Original, photo.ContentType, extension, original),
                new ProcessedImage(StoredImageVariant.Thumbnail, photo.ContentType, extension, thumbnail),
                new ProcessedImage(StoredImageVariant.PrivatePreview, photo.ContentType, extension, privatePreview)
            });
    }

    public ProcessedPhotoSet ProcessAvatarPhoto(IncomingPhoto photo)
    {
        var bytes = ReadAllBytes(photo.Content);
        using var image = Image.Load<Rgba32>(bytes);
        NormalizeMetadataAndSize(image, ThumbnailSide);
        image.Mutate(operation => operation.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Crop,
            Size = new Size(ThumbnailSide, ThumbnailSide)
        }));

        var extension = ExtensionFor(photo.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var original = Encode(image, photo.ContentType);
        var thumbnail = Encode(ResizeClone(image, ThumbnailSide), photo.ContentType);

        return new ProcessedPhotoSet(
            fileName,
            photo.ContentType,
            original.LongLength,
            AverageHash(image),
            new[]
            {
                new ProcessedImage(StoredImageVariant.Original, photo.ContentType, extension, original),
                new ProcessedImage(StoredImageVariant.Thumbnail, photo.ContentType, extension, thumbnail)
            });
    }

    public double ComputeGarmentDeskewAngle(byte[] cutoutPngBytes)
    {
        using var image = Image.Load<Rgba32>(cutoutPngBytes);
        return GarmentDeskew.ComputeCorrectionDegrees(image);
    }

    public GarmentRotationRender RenderRotatedGarment(byte[] baseCutoutPngBytes, double degrees)
    {
        using var baseImage = Image.Load<Rgba32>(baseCutoutPngBytes);
        using var rotated = RotateAndTrim(baseImage, degrees);
        using var thumbnailImage = ResizeClone(rotated, ThumbnailSide);
        var cutout = EncodePng(rotated);
        var thumbnail = EncodePng(thumbnailImage);
        var mask = CreateSegmentationMask(rotated);
        var hash = AverageHash(rotated);
        return new GarmentRotationRender(cutout, thumbnail, mask, hash);
    }

    private static Image<Rgba32> RotateAndTrim(Image<Rgba32> source, double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        if (Math.Abs(normalized) < 0.01)
        {
            return source.Clone();
        }

        var rotated = source.Clone(operation => operation.Rotate((float)normalized));
        if (OpaqueBounds(rotated) is { } bounds
            && bounds.Width > 0
            && bounds.Height > 0
            && (bounds.Width != rotated.Width || bounds.Height != rotated.Height))
        {
            rotated.Mutate(operation => operation.Crop(bounds));
        }

        return rotated;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var wrapped = degrees % 360d;
        if (wrapped < 0)
        {
            wrapped += 360d;
        }

        return wrapped > 180d ? wrapped - 360d : wrapped;
    }

    private static Rectangle? OpaqueBounds(Image<Rgba32> image)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = -1;
        var maxY = -1;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A >= 16)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        });

        return maxX < 0 ? null : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void NormalizeMetadataAndSize(Image image, int maxSide)
    {
        image.Mutate(operation => operation.AutoOrient().Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxSide, maxSide)
        }));
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.XmpProfile = null;
    }

    private static Image<Rgba32> ResizeClone(Image<Rgba32> source, int maxSide)
    {
        return source.Clone(operation => operation.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxSide, maxSide)
        }));
    }

    private Image<Rgba32> CreateGarmentCutout(string fileName, string contentType, byte[] imageBytes)
    {
        var result = _garmentExtraction.ExtractGarments(new GarmentExtractionRequest(fileName, contentType, imageBytes));
        var item = result.Items.Count > 0
            ? result.Items[0]
            : throw new InvalidOperationException("Garment extraction did not return an item.");
        return Image.Load<Rgba32>(item.ImageBytes);
    }

    private static byte[] CreateSegmentationMask(Image<Rgba32> cutout)
    {
        using var mask = new Image<Rgba32>(cutout.Width, cutout.Height);
        cutout.ProcessPixelRows(mask, (sourceAccessor, targetAccessor) =>
        {
            for (var y = 0; y < sourceAccessor.Height; y++)
            {
                var sourceRow = sourceAccessor.GetRowSpan(y);
                var targetRow = targetAccessor.GetRowSpan(y);
                for (var x = 0; x < sourceRow.Length; x++)
                {
                    targetRow[x] = sourceRow[x].A == 0
                        ? new Rgba32(0, 0, 0, 255)
                        : new Rgba32(255, 255, 255, 255);
                }
            }
        });

        return EncodePng(mask);
    }

    private static string AverageHash(Image<Rgba32> image)
    {
        using var hashImage = image.Clone(operation => operation.Resize(8, 8).Grayscale());
        var luminance = new List<byte>(64);
        hashImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    luminance.Add(row[x].R);
                }
            }
        });

        var average = luminance.Average(value => value);
        ulong bits = 0;
        for (var index = 0; index < luminance.Count; index++)
        {
            if (luminance[index] >= average)
            {
                bits |= 1UL << index;
            }
        }

        return bits.ToString("x16");
    }

    private static byte[] Encode(Image image, string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => EncodeJpeg(image),
            "image/png" => EncodePng(image),
            "image/webp" => EncodeWebp(image),
            _ => throw new InvalidOperationException("Unsupported image content type.")
        };
    }

    private static byte[] EncodeJpeg(Image image)
    {
        using var output = new MemoryStream();
        image.Save(output, new JpegEncoder { Quality = 86 });
        return output.ToArray();
    }

    private static byte[] EncodePng(Image image)
    {
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }

    private static byte[] EncodeWebp(Image image)
    {
        using var output = new MemoryStream();
        image.Save(output, new WebpEncoder { Quality = 86 });
        return output.ToArray();
    }

    private static string ExtensionFor(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported image content type.")
        };
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
