using System.Text.Json;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.Storage;

// Global hairstyle presets vendored under assets/hairstyles: manifest.json maps preset id →
// gender → asset file → name/sort order and is the single source of truth. Only files the
// manifest lists are served, so request input never selects file-system paths directly.
public sealed class ManifestHairstylePresetCatalog : IHairstylePresetCatalog
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _assetsDirectory;
    private readonly Lazy<IReadOnlyList<HairstylePreset>> _presets;

    public ManifestHairstylePresetCatalog(string assetsDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetsDirectory))
        {
            throw new ArgumentException("Hairstyle assets directory is required.", nameof(assetsDirectory));
        }

        _assetsDirectory = Path.GetFullPath(assetsDirectory);
        _presets = new Lazy<IReadOnlyList<HairstylePreset>>(LoadManifest);
    }

    public IReadOnlyList<HairstylePreset> ListHairstylePresets(UserGender gender)
    {
        return _presets.Value
            .Where(preset => preset.Gender == gender)
            .OrderBy(preset => preset.SortOrder)
            .ThenBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public StoredPhotoFile? GetHairstyleAssetFile(string assetFileName)
    {
        if (string.IsNullOrWhiteSpace(assetFileName))
        {
            return null;
        }

        var preset = _presets.Value.FirstOrDefault(candidate =>
            string.Equals(candidate.AssetFileName, assetFileName, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            return null;
        }

        var fullPath = Path.Combine(_assetsDirectory, preset.AssetFileName);
        if (!File.Exists(fullPath) || ContentTypeForExtension(Path.GetExtension(preset.AssetFileName)) is not { } contentType)
        {
            return null;
        }

        return new StoredPhotoFile(fullPath, contentType);
    }

    private IReadOnlyList<HairstylePreset> LoadManifest()
    {
        var manifestPath = Path.Combine(_assetsDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Hairstyle preset manifest not found at {manifestPath}.");
        }

        var manifest = JsonSerializer.Deserialize<HairstyleManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions);
        if (manifest?.Presets is not { Count: > 0 } entries)
        {
            throw new InvalidOperationException($"Hairstyle preset manifest lists no presets: {manifestPath}");
        }

        var presets = new List<HairstylePreset>(entries.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.File))
            {
                throw new InvalidOperationException($"Hairstyle preset entries need id, name, and file: {manifestPath}");
            }

            if (!Enum.TryParse<UserGender>(entry.Gender, ignoreCase: false, out var gender))
            {
                throw new InvalidOperationException($"Hairstyle preset '{entry.Id}' has unsupported gender '{entry.Gender}'.");
            }

            if (entry.File != Path.GetFileName(entry.File))
            {
                throw new InvalidOperationException($"Hairstyle preset '{entry.Id}' must reference a bare file name, got '{entry.File}'.");
            }

            if (!seenIds.Add(entry.Id))
            {
                throw new InvalidOperationException($"Hairstyle preset id '{entry.Id}' is duplicated.");
            }

            presets.Add(new HairstylePreset(entry.Id.Trim(), gender, entry.Name.Trim(), entry.File.Trim(), entry.SortOrder));
        }

        return presets;
    }

    private static string? ContentTypeForExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
    }

    private sealed record HairstyleManifest(IReadOnlyList<HairstyleManifestEntry>? Presets);

    private sealed record HairstyleManifestEntry(string? Id, string? Gender, string? Name, string? File, int SortOrder);
}
