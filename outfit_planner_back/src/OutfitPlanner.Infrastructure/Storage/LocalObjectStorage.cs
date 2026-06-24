using System.Security.Cryptography;
using System.Text;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class LocalObjectStorage : IObjectStorage
{
    private readonly string _storageRoot;
    private readonly byte[] _signingKey;
    private readonly string? _publicOrigin;

    public LocalObjectStorage(string storageRoot, string? signingSecret = null, string? publicOrigin = null)
    {
        _storageRoot = Path.GetFullPath(storageRoot);
        _signingKey = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(signingSecret)
            ? "local-object-storage-development-signing-key"
            : signingSecret);
        _publicOrigin = NormalizePublicOrigin(publicOrigin);
    }

    public StoredObject PutObject(ObjectStoragePutRequest request)
    {
        var objectKey = NormalizeObjectKey(request.ObjectKey);
        var fullPath = FullPathForObjectKey(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        request.Content.CopyTo(output);

        return new StoredObject(objectKey, request.ContentType, output.Length, request.Private);
    }

    public StoredObjectFile? GetObject(string objectKey)
    {
        objectKey = NormalizeObjectKey(objectKey);
        var fullPath = FullPathForObjectKey(objectKey);
        return File.Exists(fullPath)
            ? new StoredObjectFile(fullPath, ContentTypeForExtension(Path.GetExtension(fullPath)))
            : null;
    }

    public bool DeleteObject(string objectKey)
    {
        objectKey = NormalizeObjectKey(objectKey);
        var fullPath = FullPathForObjectKey(objectKey);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        File.Delete(fullPath);
        return true;
    }

    public int DeletePrefix(string prefix)
    {
        prefix = NormalizeObjectKey(prefix).TrimEnd('/');
        var prefixPath = FullPathForObjectKey(prefix);
        if (!Directory.Exists(prefixPath) && !File.Exists(prefixPath))
        {
            return 0;
        }

        var deleted = 0;
        var searchRoot = Directory.Exists(prefixPath) ? prefixPath : Path.GetDirectoryName(prefixPath)!;
        foreach (var file in Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_storageRoot, file).Replace('\\', '/');
            if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(file);
            deleted++;
        }

        return deleted;
    }

    public string CreateSignedReadUrl(string objectKey, TimeSpan lifetime)
    {
        objectKey = NormalizeObjectKey(objectKey);
        var expires = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var signature = Sign(objectKey, expires);
        var escapedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        var signedPath = $"/api/storage/signed/{escapedKey}?expires={expires}&signature={Uri.EscapeDataString(signature)}";
        return _publicOrigin is null ? signedPath : $"{_publicOrigin}{signedPath}";
    }

    public StoredObjectFile? GetSignedObject(string objectKey, long expiresUnixTimeSeconds, string signature, DateTimeOffset now)
    {
        if (expiresUnixTimeSeconds <= now.ToUnixTimeSeconds())
        {
            return null;
        }

        objectKey = NormalizeObjectKey(objectKey);
        return FixedTimeEquals(signature, Sign(objectKey, expiresUnixTimeSeconds))
            ? GetObject(objectKey)
            : null;
    }

    private string FullPathForObjectKey(string objectKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Object key escaped storage root.");
        }

        return fullPath;
    }

    private string Sign(string objectKey, long expiresUnixTimeSeconds)
    {
        using var hmac = new HMACSHA256(_signingKey);
        var payload = Encoding.UTF8.GetBytes($"{objectKey}\n{expiresUnixTimeSeconds}");
        return Convert.ToBase64String(hmac.ComputeHash(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new InvalidOperationException("Object key is required.");
        }

        var normalized = objectKey.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException("Object key is not safe.");
        }

        return normalized;
    }

    private static string? NormalizePublicOrigin(string? publicOrigin)
    {
        if (string.IsNullOrWhiteSpace(publicOrigin))
        {
            return null;
        }

        var trimmed = publicOrigin.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Public origin must be an absolute HTTP or HTTPS origin.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string ContentTypeForExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
