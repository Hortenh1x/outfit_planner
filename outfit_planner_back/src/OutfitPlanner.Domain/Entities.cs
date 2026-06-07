namespace OutfitPlanner.Domain;

public sealed record BodyReferencePhoto(
    Guid Id,
    string UserId,
    string ImageUrl,
    DateTimeOffset CreatedAt);

public sealed record GarmentItem(
    Guid Id,
    string UserId,
    string Name,
    GarmentCategory Category,
    BodyZone BodyZone,
    string ImageUrl,
    string ThumbnailUrl,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);

public sealed record OutfitItem(
    Guid GarmentId,
    string Name,
    GarmentCategory Category,
    BodyZone BodyZone,
    string ThumbnailUrl);

public sealed record Outfit(
    Guid Id,
    string UserId,
    string Name,
    IReadOnlyList<OutfitItem> Items,
    string? ClothesOnlyPreviewUrl,
    string? PersonPreviewUrl,
    DateTimeOffset CreatedAt);

public sealed record ScheduledOutfit(
    Guid Id,
    string UserId,
    DateOnly Date,
    Guid OutfitId,
    DateTimeOffset CreatedAt);

public sealed record TryOnJob(
    Guid Id,
    string UserId,
    Guid OutfitId,
    string BodyReferencePhotoUrl,
    TryOnStatus Status,
    string? ProviderJobId,
    string? OutputImageUrl,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ShareLink(
    string Token,
    string UserId,
    Guid OutfitId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);
