namespace OutfitPlanner.Application.Services;

public sealed record UpdateOutfitCommand(
    string? Name = null,
    IReadOnlyList<Guid>? GarmentIds = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? Occasion = null,
    bool? IsFavorite = null,
    bool? IsArchived = null);
