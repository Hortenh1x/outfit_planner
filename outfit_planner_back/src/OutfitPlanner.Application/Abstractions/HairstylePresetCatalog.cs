using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public interface IHairstylePresetCatalog
{
    // Global presets for one gender, ordered for display. The catalog is app-owned and static;
    // there is no per-user hairstyle data.
    IReadOnlyList<HairstylePreset> ListHairstylePresets(UserGender gender);

    // Resolves a preset asset file for serving. Only files listed in the preset manifest are
    // resolvable, so arbitrary path input never reaches the file system. Null when unknown.
    StoredPhotoFile? GetHairstyleAssetFile(string assetFileName);
}
