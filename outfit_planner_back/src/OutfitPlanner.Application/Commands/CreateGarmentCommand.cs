using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record CreateGarmentCommand(
    string UserId,
    string Name,
    GarmentCategory Category,
    string ImageUrl,
    string? ThumbnailUrl,
    IReadOnlyList<string> Tags);
