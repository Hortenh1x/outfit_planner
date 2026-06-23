using System.Diagnostics;
using System.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed record BackgroundRemovalRequest(string FileName, string ContentType, byte[] ImageBytes);

public sealed record BackgroundRemovalResult(byte[] ImageBytes, string ContentType, string ProviderName);

public interface IBackgroundRemovalProvider
{
    string Name { get; }

    BackgroundRemovalResult RemoveBackground(BackgroundRemovalRequest request);
}

public sealed record GarmentExtractionRequest(string FileName, string ContentType, byte[] ImageBytes);

public sealed record GarmentExtractionCandidate(
    string FileName,
    string ContentType,
    byte[] ImageBytes,
    string? SuggestedCategory,
    decimal Confidence);

public sealed record GarmentExtractionResult(IReadOnlyList<GarmentExtractionCandidate> Items, string ProviderName);

public interface IGarmentExtractionProvider
{
    string Name { get; }

    GarmentExtractionResult ExtractGarments(GarmentExtractionRequest request);
}

public sealed class SingleGarmentExtractionProvider : IGarmentExtractionProvider
{
    private readonly IBackgroundRemovalProvider _backgroundRemoval;

    public SingleGarmentExtractionProvider(IBackgroundRemovalProvider backgroundRemoval)
    {
        _backgroundRemoval = backgroundRemoval;
    }

    public string Name => "single-garment";

    public GarmentExtractionResult ExtractGarments(GarmentExtractionRequest request)
    {
        var cutout = _backgroundRemoval.RemoveBackground(new BackgroundRemovalRequest(
            request.FileName,
            request.ContentType,
            request.ImageBytes));

        return new GarmentExtractionResult(
            new[]
            {
                new GarmentExtractionCandidate(
                    request.FileName,
                    cutout.ContentType,
                    cutout.ImageBytes,
                    SuggestedCategory: "Top",
                    Confidence: 1m)
            },
            Name);
    }
}

public sealed class SimpleBackgroundRemovalProvider : IBackgroundRemovalProvider
{
    public string Name => "simple";

    public BackgroundRemovalResult RemoveBackground(BackgroundRemovalRequest request)
    {
        using var image = Image.Load<Rgba32>(request.ImageBytes);
        ApplySimpleBackgroundCutout(image);
        return new BackgroundRemovalResult(EncodePng(image), "image/png", Name);
    }

    private static void ApplySimpleBackgroundCutout(Image<Rgba32> image)
    {
        var background = EstimateBackground(image);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (ColorDistance(pixel, background) < 44)
                    {
                        row[x] = new Rgba32(pixel.R, pixel.G, pixel.B, 0);
                    }
                }
            }
        });
    }

    private static Rgba32 EstimateBackground(Image<Rgba32> image)
    {
        var samples = new List<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            samples.Add(accessor.GetRowSpan(0)[0]);
            samples.Add(accessor.GetRowSpan(0)[accessor.Width - 1]);
            samples.Add(accessor.GetRowSpan(accessor.Height - 1)[0]);
            samples.Add(accessor.GetRowSpan(accessor.Height - 1)[accessor.Width - 1]);
        });

        return new Rgba32(
            (byte)samples.Average(pixel => pixel.R),
            (byte)samples.Average(pixel => pixel.G),
            (byte)samples.Average(pixel => pixel.B),
            255);
    }

    private static double ColorDistance(Rgba32 left, Rgba32 right)
    {
        var r = left.R - right.R;
        var g = left.G - right.G;
        var b = left.B - right.B;
        return Math.Sqrt((r * r) + (g * g) + (b * b));
    }

    private static byte[] EncodePng(Image image)
    {
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

public sealed record RembgBackgroundRemovalSettings(
    string ExecutablePath,
    string ModelName,
    TimeSpan Timeout,
    string? ModelHome = null);

public sealed class RembgBackgroundRemovalProvider : IBackgroundRemovalProvider
{
    private readonly RembgBackgroundRemovalSettings _settings;

    public RembgBackgroundRemovalProvider(RembgBackgroundRemovalSettings settings)
    {
        _settings = settings;
    }

    public string Name => "rembg";

    public BackgroundRemovalResult RemoveBackground(BackgroundRemovalRequest request)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "outfit-planner-rembg", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputPath = Path.Combine(tempDir, $"input{ExtensionFor(request.ContentType)}");
            var outputPath = Path.Combine(tempDir, "output.png");
            File.WriteAllBytes(inputPath, request.ImageBytes);

            using var process = StartRembg(inputPath, outputPath);
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            var timeout = _settings.Timeout <= TimeSpan.Zero
                ? Timeout.Infinite
                : (int)Math.Min(_settings.Timeout.TotalMilliseconds, int.MaxValue);

            if (!process.WaitForExit(timeout))
            {
                TryKill(process);
                throw new InvalidOperationException($"Background removal provider rembg timed out after {_settings.Timeout.TotalSeconds:0} seconds.");
            }

            process.WaitForExit();
            var stdout = standardOutput.GetAwaiter().GetResult();
            var stderr = standardError.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Background removal provider rembg failed with exit code {process.ExitCode}: {TrimForError(stderr.Length > 0 ? stderr : stdout)}");
            }

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException("Background removal provider rembg did not create an output image.");
            }

            return new BackgroundRemovalResult(File.ReadAllBytes(outputPath), "image/png", Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private Process StartRembg(string inputPath, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(_settings.ExecutablePath) ? "rembg" : _settings.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("i");
        if (!string.IsNullOrWhiteSpace(_settings.ModelName))
        {
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add(_settings.ModelName);
        }

        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add(outputPath);

        if (!string.IsNullOrWhiteSpace(_settings.ModelHome))
        {
            startInfo.Environment["U2NET_HOME"] = _settings.ModelHome;
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Background removal provider rembg could not be started.");
    }

    private static string ExtensionFor(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".img"
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string TrimForError(string value)
    {
        value = value.Trim();
        return value.Length <= 800 ? value : value[..800];
    }
}

public sealed record HttpBackgroundRemovalSettings(
    string Endpoint,
    string ApiKey,
    string ApiKeyHeader,
    string ApiKeyPrefix,
    string ImageFieldName,
    TimeSpan Timeout);

public sealed class HttpBackgroundRemovalProvider : IBackgroundRemovalProvider
{
    private readonly HttpClient _client;
    private readonly HttpBackgroundRemovalSettings _settings;

    public HttpBackgroundRemovalProvider(HttpClient client, HttpBackgroundRemovalSettings settings)
    {
        _client = client;
        _settings = settings;
        if (settings.Timeout > TimeSpan.Zero)
        {
            _client.Timeout = settings.Timeout;
        }
    }

    public string Name => "http";

    public BackgroundRemovalResult RemoveBackground(BackgroundRemovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException("BackgroundRemoval:Http:Endpoint must be configured.");
        }

        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(request.ImageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        content.Add(imageContent, FieldName(), SafeFileName(request.FileName));

        using var message = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
        {
            Content = content
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
        AddApiKey(message);

        using var response = _client.Send(message);
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException($"Background removal provider http returned {(int)response.StatusCode}: {TrimForError(detail)}");
        }

        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Background removal provider http returned an empty image.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Background removal provider http returned non-image content type {contentType}.");
        }

        return new BackgroundRemovalResult(bytes, contentType, Name);
    }

    private void AddApiKey(HttpRequestMessage message)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.ApiKeyHeader))
        {
            return;
        }

        message.Headers.TryAddWithoutValidation(_settings.ApiKeyHeader, $"{_settings.ApiKeyPrefix}{_settings.ApiKey}");
    }

    private string FieldName()
    {
        return string.IsNullOrWhiteSpace(_settings.ImageFieldName) ? "image_file" : _settings.ImageFieldName;
    }

    private static string SafeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safe) ? "image.png" : safe;
    }

    private static string TrimForError(string value)
    {
        value = value.Trim();
        return value.Length <= 800 ? value : value[..800];
    }
}
