namespace OutfitPlanner.Domain;

// A global hairstyle preset — a curated, openly licensed hair asset offered per gender. It is
// not a user garment: presets are app-owned, occupy the Head zone conceptually, come in a
// single hair color, and are never sent to AI try-on providers.
public sealed record HairstylePreset(
    string Id,
    UserGender Gender,
    string Name,
    string AssetFileName,
    int SortOrder);
