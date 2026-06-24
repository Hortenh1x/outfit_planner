using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class TryOnOutputStorage : ITryOnOutputStorage
{
    private static readonly TimeSpan FallbackSignedUrlLifetime = TimeSpan.FromDays(30);
    private const string ObjectPrefix = "try-on-output";
    private const string LocalSignedRoutePrefix = "/api/storage/signed/";

    private readonly IObjectStorage _objects;
    private readonly HttpClient _http;

    public TryOnOutputStorage(IObjectStorage objects, HttpClient http)
    {
        _objects = objects;
        _http = http;
    }

    public async Task<string> StoreAsync(Guid jobId, string sourceImageUrl, DateTimeOffset retentionUntil, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(sourceImageUrl, UriKind.Absolute, out var sourceUri)
            || sourceUri.Scheme is not ("http" or "https"))
        {
            return sourceImageUrl;
        }

        using var response = await _http.GetAsync(sourceUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Try-on output download failed with {(int)response.StatusCode}.");
        }

        var contentType = ContentType(response.Content.Headers.ContentType?.MediaType, sourceUri);
        var objectKey = $"{ObjectPrefix}/{jobId:N}{ExtensionForContentType(contentType)}";
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var stored = _objects.PutObject(new ObjectStoragePutRequest(
            objectKey,
            contentType,
            stream,
            Private: true,
            RetentionUntil: retentionUntil));

        return _objects.CreateSignedReadUrl(stored.ObjectKey, SignedUrlLifetime(retentionUntil));
    }

    public bool DeleteOutput(string outputImageUrl)
    {
        var objectKey = TryReadTryOnOutputObjectKey(outputImageUrl);
        return objectKey is not null && _objects.DeleteObject(objectKey);
    }

    private static TimeSpan SignedUrlLifetime(DateTimeOffset retentionUntil)
    {
        var lifetime = retentionUntil - DateTimeOffset.UtcNow;
        return lifetime > TimeSpan.Zero ? lifetime : FallbackSignedUrlLifetime;
    }

    private static string ContentType(string? responseContentType, Uri sourceUri)
    {
        return responseContentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            _ => ExtensionForPath(sourceUri.AbsolutePath) switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/png"
            }
        };
    }

    private static string ExtensionForContentType(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png"
        };
    }

    private static string ExtensionForPath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant();
    }

    private static string? TryReadTryOnOutputObjectKey(string outputImageUrl)
    {
        if (string.IsNullOrWhiteSpace(outputImageUrl))
        {
            return null;
        }

        var path = Uri.TryCreate(outputImageUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : outputImageUrl.Split('?', 2)[0];

        string candidate;
        if (path.StartsWith(LocalSignedRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate = Uri.UnescapeDataString(path[LocalSignedRoutePrefix.Length..].Trim('/'));
        }
        else
        {
            var marker = $"/{ObjectPrefix}/";
            var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            candidate = markerIndex >= 0
                ? path[(markerIndex + 1)..]
                : path.TrimStart('/');
        }

        return candidate.StartsWith(ObjectPrefix + "/", StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }
}
