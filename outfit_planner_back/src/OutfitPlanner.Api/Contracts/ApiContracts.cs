using OutfitPlanner.Domain;

namespace OutfitPlanner.Api.Contracts;

public sealed record CreateBodyReferencePhotoRequest(string ImageUrl);

public sealed record CreateGarmentRequest(
    string Name,
    GarmentCategory Category,
    string ImageUrl,
    string? ThumbnailUrl,
    IReadOnlyList<string>? Tags,
    string? PrimaryColor,
    IReadOnlyList<string>? SecondaryColors,
    string? Material,
    string? Brand,
    string? Size,
    IReadOnlyList<string>? Season,
    int? WeatherMinTemp,
    int? WeatherMaxTemp,
    IReadOnlyList<string>? Occasion,
    int? FormalityScore,
    int? WarmthScore,
    int? ComfortScore,
    bool? IsFavorite,
    bool? IsArchived,
    DateTimeOffset? LastWornAt,
    string? LaundryStatus);

public sealed record UpdateGarmentRequest(
    string? Name,
    GarmentCategory? Category,
    IReadOnlyList<string>? Tags,
    string? PrimaryColor,
    IReadOnlyList<string>? SecondaryColors,
    string? Material,
    string? Brand,
    string? Size,
    IReadOnlyList<string>? Season,
    int? WeatherMinTemp,
    int? WeatherMaxTemp,
    IReadOnlyList<string>? Occasion,
    int? FormalityScore,
    int? WarmthScore,
    int? ComfortScore,
    bool? IsFavorite,
    bool? IsArchived,
    DateTimeOffset? LastWornAt,
    string? LaundryStatus);

public sealed record CreateOutfitRequest(string Name, IReadOnlyList<Guid> GarmentIds);

public sealed record UpdateOutfitRequest(
    string? Name,
    IReadOnlyList<Guid>? GarmentIds,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<string>? Occasion,
    bool? IsFavorite,
    bool? IsArchived);

public sealed record ScheduleOutfitRequest(DateOnly Date, Guid OutfitId);

public sealed record EstimateTryOnRequest(
    string BodyReferencePhotoUrl,
    TryOnMode TryOnMode,
    Guid? BodyReferencePhotoId = null);

public sealed record StartTryOnRequest(
    string BodyReferencePhotoUrl,
    bool ConsentAccepted,
    TryOnMode TryOnMode,
    int ConfirmedCredits,
    string ConfirmedCacheKey,
    Guid? BodyReferencePhotoId = null);

public sealed record TryOnEstimateItemResponse(
    Guid GarmentId,
    string Name,
    GarmentCategory Category,
    BodyZone BodyZone,
    string ThumbnailUrl);

public sealed record TryOnEstimateResponse(
    TryOnMode Mode,
    string Provider,
    IReadOnlyList<TryOnEstimateItemResponse> BodyTryOnItems,
    IReadOnlyList<TryOnEstimateItemResponse> VisualOnlyItems,
    IReadOnlyList<Guid> IncludedGarmentIds,
    IReadOnlyList<Guid> ExcludedGarmentIds,
    int EstimatedCredits,
    bool IsAvailable,
    bool RequiresAi,
    bool RequiresPremiumConfirmation,
    string CacheKey,
    bool HasCachedResult,
    string Summary,
    IReadOnlyList<string> Warnings);

public sealed record ShareLinkResponse(string Token, string Url);

public sealed record SharedOutfitResponse(
    Guid Id,
    string Name,
    IReadOnlyList<OutfitItem> Items,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Occasion,
    bool IsFavorite,
    bool IsArchived,
    string? ClothesOnlyPreviewUrl,
    string? PersonPreviewUrl,
    DateTimeOffset CreatedAt);

public sealed record UploadedPhotoResponse(string FileName, string ContentType, long Length, string Url);

public sealed record RegisterRequest(string Email, string Password, string RepeatPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record EmailVerificationRequest(string Email);

public sealed record TokenRequest(string Token);

public sealed record PasswordResetRequest(string Email);

public sealed record PasswordResetConfirmRequest(string Token, string Password, string RepeatPassword);

public sealed record AuthUserResponse(string Id, string? Email, string DisplayName);

public sealed record AuthSessionResponse(AuthUserResponse User, DateTimeOffset ExpiresAt);

public sealed record AuthProviderResponse(string Id, string Label, bool Configured, string Flow);
