using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class LocalPhotoStorage : IPhotoStorage, IStoredPhotoReader, IStoredPhotoDeletion
{
    private readonly string _storageRoot;

    public LocalPhotoStorage(string storageRoot)
    {
        _storageRoot = Path.GetFullPath(storageRoot);
    }

    public StoredPhoto SaveGarmentPhoto(IncomingPhoto photo)
    {
        return SavePhoto(photo, "garments", "/uploads/garments");
    }

    public StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo)
    {
        return SavePhoto(photo, "body-reference-photos", "/uploads/body-reference-photos");
    }

    public StoredPhotoFile? GetGarmentPhoto(string fileName)
    {
        return GetPhoto("garments", fileName);
    }

    public StoredPhotoFile? GetBodyReferencePhoto(string fileName)
    {
        return GetPhoto("body-reference-photos", fileName);
    }

    public bool DeleteGarmentPhoto(string photoUrl)
    {
        return DeletePhoto("garments", "/uploads/garments/", photoUrl);
    }

    public bool DeleteBodyReferencePhoto(string photoUrl)
    {
        return DeletePhoto("body-reference-photos", "/uploads/body-reference-photos/", photoUrl);
    }

    private StoredPhoto SavePhoto(IncomingPhoto photo, string storageFolder, string publicBasePath)
    {
        var folderPath = Path.Combine(_storageRoot, storageFolder);
        Directory.CreateDirectory(folderPath);

        var extension = ExtensionFor(photo.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        photo.Content.CopyTo(output);

        return new StoredPhoto(fileName, photo.ContentType, photo.Length, $"{publicBasePath}/{fileName}");
    }

    private StoredPhotoFile? GetPhoto(string storageFolder, string fileName)
    {
        if (!IsSafeStoredFileName(fileName))
        {
            return null;
        }

        var folderPath = Path.GetFullPath(Path.Combine(_storageRoot, storageFolder));
        var fullPath = Path.GetFullPath(Path.Combine(folderPath, fileName));
        if (!fullPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return null;
        }

        return ContentTypeForExtension(Path.GetExtension(fileName)) is { } contentType
            ? new StoredPhotoFile(fullPath, contentType)
            : null;
    }

    private bool DeletePhoto(string storageFolder, string publicPathPrefix, string photoUrl)
    {
        var fileName = FileNameFromPublicUrl(photoUrl, publicPathPrefix);
        if (fileName is null || GetPhoto(storageFolder, fileName) is not { } photo)
        {
            return false;
        }

        File.Delete(photo.FullPath);
        return true;
    }

    private static string? FileNameFromPublicUrl(string photoUrl, string publicPathPrefix)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        var path = Uri.TryCreate(photoUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : photoUrl;

        if (!path.StartsWith(publicPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fileName = Path.GetFileName(path);
        return IsSafeStoredFileName(fileName) ? fileName : null;
    }

    private static bool IsSafeStoredFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName == Path.GetFileName(fileName)
            && ContentTypeForExtension(Path.GetExtension(fileName)) is not null;
    }

    private static string ExtensionFor(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported photo content type.")
        };
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
