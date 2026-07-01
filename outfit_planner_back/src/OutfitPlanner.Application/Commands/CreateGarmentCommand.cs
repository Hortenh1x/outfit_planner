using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record CreateGarmentCommand(
    string UserId,
    string Name,
    GarmentCategory Category,
    string ImageUrl,
    string? ThumbnailUrl,
    IReadOnlyList<string> Tags,
    string? PrimaryColor = null,
    IReadOnlyList<string>? SecondaryColors = null,
    string? Material = null,
    string? Brand = null,
    string? Size = null,
    IReadOnlyList<string>? Season = null,
    int? WeatherMinTemp = null,
    int? WeatherMaxTemp = null,
    IReadOnlyList<string>? Occasion = null,
    int? FormalityScore = null,
    int? WarmthScore = null,
    int? ComfortScore = null,
    bool IsFavorite = false,
    bool IsArchived = false,
    DateTimeOffset? LastWornAt = null,
    string? LaundryStatus = null,
    string? PerceptualHash = null,
    bool BackgroundRemovalPending = false);
