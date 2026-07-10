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
    // Decompression-bomb guard: a small, highly compressible file can declare an enormous
    // canvas that would explode into gigabytes of pixel buffer on decode. Reject anything
    // above this pixel count from the header before allocating the full image.
    private const long MaxDecodedPixels = 100_000_000; // 100 megapixels

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

    // Loads user-supplied image bytes only after confirming the declared dimensions are
    // sane (header-only Identify), so an oversized canvas is rejected before it can
    // allocate a huge pixel buffer.
    private static Image<Rgba32> LoadWithinLimits(byte[] bytes)
    {
        var info = Image.Identify(bytes);
        if (info is not null && (long)info.Width * info.Height > MaxDecodedPixels)
        {
            throw new OutfitPlanner.Domain.ValidationException(
                "Image dimensions are too large. Use an image under 100 megapixels.");
        }

        return Image.Load<Rgba32>(bytes);
    }

    public ProcessedPhotoSet ProcessGarmentPhoto(IncomingPhoto photo)
    {
        var bytes = ReadAllBytes(photo.Content);
        using var image = LoadWithinLimits(bytes);
        NormalizeMetadataAndSize(image, MaxImageSide);

        var extension = ExtensionFor(photo.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var original = Encode(image, photo.ContentType);
        using var cutoutImage = CreateGarmentCutout(photo.FileName, photo.ContentType, original);
        // Trim transparent padding so the stored cutout IS the garment's alpha bounding box;
        // relative-size rendering can then treat the image frame as the measured extent.
        var measurement = TrimToOpaqueBounds(cutoutImage);
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
            },
            measurement);
    }

    public string? ComputePerceptualHash(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return null;
        }

        using var image = Image.Load<Rgba32>(imageBytes);
        NormalizeMetadataAndSize(image, MaxImageSide);
        return AverageHash(image);
    }

    public GarmentCutoutMeasurement? MeasureGarmentCutout(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return null;
        }

        using var image = Image.Load<Rgba32>(imageBytes);
        // Cutouts carry no EXIF, but the backfill also measures legacy originals, whose
        // orientation may still live in metadata rather than pixels.
        image.Mutate(operation => operation.AutoOrient());
        return OpaqueBounds(image) is { } bounds
            ? new GarmentCutoutMeasurement(bounds.Width, bounds.Height)
            : null;
    }

    public ProcessedPhotoSet ProcessGarmentOriginal(IncomingPhoto photo)
    {
        var bytes = ReadAllBytes(photo.Content);
        using var image = LoadWithinLimits(bytes);
        NormalizeMetadataAndSize(image, MaxImageSide);

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

    public ProcessedPhotoSet ProcessBodyReferencePhoto(IncomingPhoto photo)
    {
        var bytes = ReadAllBytes(photo.Content);
        using var image = LoadWithinLimits(bytes);
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
        using var image = LoadWithinLimits(bytes);
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
        using var rotated = RotateClone(baseImage, degrees);
        var measurement = TrimToOpaqueBounds(rotated);
        using var thumbnailImage = ResizeClone(rotated, ThumbnailSide);
        var cutout = EncodePng(rotated);
        var thumbnail = EncodePng(thumbnailImage);
        var mask = CreateSegmentationMask(rotated);
        var hash = AverageHash(rotated);
        return new GarmentRotationRender(cutout, thumbnail, mask, hash, measurement);
    }

    private static Image<Rgba32> RotateClone(Image<Rgba32> source, double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        return Math.Abs(normalized) < 0.01
            ? source.Clone()
            : source.Clone(operation => operation.Rotate((float)normalized));
    }

    // Crops the image in place to its alpha bounding box and returns the resulting measurement,
    // or null (and no crop) when the image has no opaque pixels at all.
    private static GarmentCutoutMeasurement? TrimToOpaqueBounds(Image<Rgba32> image)
    {
        if (OpaqueBounds(image) is not { } bounds)
        {
            return null;
        }

        if (bounds.Width != image.Width || bounds.Height != image.Height)
        {
            image.Mutate(operation => operation.Crop(bounds));
        }

        return new GarmentCutoutMeasurement(bounds.Width, bounds.Height);
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

    // Bounding box of the garment's main opaque mass. Background removal is imperfect (the local
    // Simple keyer especially leaves textured-floor grain opaque), and a naive min/max over every
    // opaque pixel would balloon the box out to a stray speck in a corner — making the garment
    // measure far bigger than it visually is and render tiny inside its figure slot. So label the
    // connected opaque regions (8-connectivity) and keep the union of the substantial ones (each
    // ≥12% of the largest, which tolerates a garment split into a few parts by the cutout) while
    // dropping scattered specks. The whole connected garment is always kept, so no real garment
    // part is ever cropped. Returns null when nothing is opaque.
    private static Rectangle? OpaqueBounds(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var opaque = new bool[width * height];
        var opaqueCount = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var rowBase = y * width;
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A >= 16)
                    {
                        opaque[rowBase + x] = true;
                        opaqueCount++;
                    }
                }
            }
        });

        if (opaqueCount == 0)
        {
            return null;
        }

        // Union-find over opaque pixels, linking each to its already-scanned neighbours (left, up,
        // up-left, up-right) for 8-connectivity.
        var parent = new int[width * height];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowBase + x;
                if (!opaque[index])
                {
                    continue;
                }

                if (x > 0 && opaque[index - 1])
                {
                    UnionCells(parent, index, index - 1);
                }
                if (y > 0)
                {
                    if (opaque[index - width])
                    {
                        UnionCells(parent, index, index - width);
                    }
                    if (x > 0 && opaque[index - width - 1])
                    {
                        UnionCells(parent, index, index - width - 1);
                    }
                    if (x < width - 1 && opaque[index - width + 1])
                    {
                        UnionCells(parent, index, index - width + 1);
                    }
                }
            }
        }

        // Component sizes, and the largest.
        var sizes = new Dictionary<int, int>();
        var largest = 0;
        for (var index = 0; index < opaque.Length; index++)
        {
            if (!opaque[index])
            {
                continue;
            }

            var root = FindCell(parent, index);
            var size = sizes.TryGetValue(root, out var current) ? current + 1 : 1;
            sizes[root] = size;
            if (size > largest)
            {
                largest = size;
            }
        }

        // Keep components that are a substantial fraction of the largest; drop small scattered specks.
        var keepThreshold = Math.Max(24, (int)(largest * 0.12));
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y++)
        {
            var rowBase = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowBase + x;
                if (!opaque[index] || sizes[FindCell(parent, index)] < keepThreshold)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        return maxX < 0 ? null : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static int FindCell(int[] parent, int node)
    {
        while (parent[node] != node)
        {
            parent[node] = parent[parent[node]];
            node = parent[node];
        }

        return node;
    }

    private static void UnionCells(int[] parent, int a, int b)
    {
        var rootA = FindCell(parent, a);
        var rootB = FindCell(parent, b);
        if (rootA != rootB)
        {
            parent[rootA] = rootB;
        }
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
