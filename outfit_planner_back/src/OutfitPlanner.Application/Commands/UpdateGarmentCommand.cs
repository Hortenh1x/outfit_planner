using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record UpdateGarmentCommand(
    string? Name = null,
    GarmentCategory? Category = null,
    IReadOnlyList<string>? Tags = null,
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
    bool? IsFavorite = null,
    bool? IsArchived = null,
    DateTimeOffset? LastWornAt = null,
    string? LaundryStatus = null);
