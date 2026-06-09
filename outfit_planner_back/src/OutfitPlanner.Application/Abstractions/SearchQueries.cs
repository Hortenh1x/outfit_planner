using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public sealed record GarmentQuery(
    GarmentCategory? Category = null,
    string? Color = null,
    string? Season = null,
    string? Search = null,
    string? Sort = null,
    int? Offset = null,
    int? Limit = null,
    bool? Favorite = null,
    bool? Archived = null,
    string? Occasion = null,
    string? Brand = null,
    string? Material = null);

public sealed record OutfitQuery(
    string? Search = null,
    string? Occasion = null,
    bool? Favorite = null,
    bool? Archived = null,
    string? Sort = null,
    int? Offset = null,
    int? Limit = null);
