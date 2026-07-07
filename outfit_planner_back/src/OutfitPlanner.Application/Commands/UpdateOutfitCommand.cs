using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record UpdateOutfitCommand(
    string? Name = null,
    IReadOnlyList<Guid>? GarmentIds = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? Occasion = null,
    bool? IsFavorite = null,
    bool? IsArchived = null,
    // Composed-figure state. Null means "leave unchanged"; an empty/whitespace
    // HairstylePresetId clears the worn hairstyle.
    string? HairstylePresetId = null,
    bool? HairstyleVisible = null,
    UserGender? SilhouetteGender = null);
