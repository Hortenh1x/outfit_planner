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
    string? LaundryStatus,
    string? PerceptualHash = null,
    bool? BackgroundRemovalPending = null,
    int? CutoutWidthPx = null,
    int? CutoutHeightPx = null);

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
    string? LaundryStatus,
    double? RotationDegrees = null);

public sealed record CreateOutfitRequest(
    string Name,
    IReadOnlyList<Guid> GarmentIds,
    // Composed-figure state; null keeps legacy behavior (no hairstyle, default silhouette).
    string? HairstylePresetId = null,
    bool? HairstyleVisible = null,
    UserGender? SilhouetteGender = null);

public sealed record UpdateOutfitRequest(
    string? Name,
    IReadOnlyList<Guid>? GarmentIds,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<string>? Occasion,
    bool? IsFavorite,
    bool? IsArchived,
    // Null leaves the worn hairstyle unchanged; an empty string clears it.
    string? HairstylePresetId = null,
    bool? HairstyleVisible = null,
    UserGender? SilhouetteGender = null);

public sealed record ScheduleOutfitRequest(DateOnly Date, Guid OutfitId);

public sealed record EstimateTryOnRequest(
    string? BodyReferencePhotoUrl,
    TryOnMode TryOnMode,
    Guid? BodyReferencePhotoId = null);

public sealed record StartTryOnRequest(
    string? BodyReferencePhotoUrl,
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
    IReadOnlyList<string> Warnings,
    // Paywall surface: the mode is blocked only by the account's plan (offer an upgrade),
    // and the account's AI-credit balance so the UI can warn before confirming.
    bool RequiresUpgrade = false,
    bool CreditsUnlimited = false,
    int? CreditBalance = null);

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
    DateTimeOffset CreatedAt,
    // Composed-figure state so the shared view reconstructs exactly what the Builder shows.
    string? HairstylePresetId = null,
    bool HairstyleVisible = true,
    UserGender? SilhouetteGender = null,
    string? HairstyleAssetUrl = null);

public sealed record UploadedPhotoResponse(
    string FileName,
    string ContentType,
    long Length,
    string Url,
    string? OriginalUrl,
    string? ThumbnailUrl,
    string? CutoutUrl,
    string? MaskUrl,
    string? PerceptualHash,
    // Alpha-bounding-box size of the processed cutout; null on the original-only fast path.
    int? CutoutWidthPx = null,
    int? CutoutHeightPx = null);

// Garment auto-tagging: the client sends the upload-response image URL plus the account's
// known tags; the server classifies a clean cutout into prefill suggestions. All fields are
// suggestions the user can override; an unavailable tagger returns IsAvailable=false with
// empty suggestions and the upload flow is unchanged.
public sealed record ClassifyGarmentPhotoRequest(string ImageUrl, IReadOnlyList<string>? KnownTags);

public sealed record AutoTagColorResponse(string Name, string Hex, double Confidence);

public sealed record AutoTagSuggestionResponse(string Value, double Confidence);

public sealed record GarmentAutoTagResponse(
    bool IsAvailable,
    string Provider,
    GarmentCategory? Category,
    double CategoryConfidence,
    IReadOnlyList<AutoTagColorResponse> Colors,
    IReadOnlyList<AutoTagSuggestionResponse> Seasons,
    IReadOnlyList<AutoTagSuggestionResponse> Tags);

public sealed record HairstylePresetResponse(
    string Id,
    string Name,
    UserGender Gender,
    int SortOrder,
    string AssetUrl);

public sealed record RegisterRequest(string Email, string Password, string RepeatPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record EmailVerificationRequest(string Email);

public sealed record TokenRequest(string Token);

public sealed record PasswordResetRequest(string Email);

public sealed record PasswordResetConfirmRequest(string Token, string Password, string RepeatPassword);

public sealed record UpdateAccountProfileRequest(string Username, UserGender? Gender);

public sealed record AuthUserResponse(
    string Id,
    string? Email,
    string DisplayName,
    string Username,
    string? AvatarUrl,
    UserGender? Gender,
    // Effective role (pinned-by-email overrides applied): the paywall foundation.
    UserRole Role);

public sealed record UpdateUserRoleRequest(UserRole Role);

public sealed record AdjustUserCreditsRequest(int Delta);

// The account's plan, current usage against its caps, and the AI-credit balance. Null caps
// mean unlimited; CreditsUnlimited marks admin accounts that bypass the ledger.
public sealed record AccountEntitlementsResponse(
    UserRole Role,
    int? MaxGarments,
    int? MaxOutfits,
    int? MaxBodyReferencePhotos,
    int GarmentCount,
    int OutfitCount,
    int BodyReferencePhotoCount,
    bool CreditsUnlimited,
    int CreditBalance,
    int MonthlyCreditAllowance,
    IReadOnlyList<TryOnMode> AllowedAiModes,
    string MaxTryOnResolution,
    bool PriorityTryOnQueue);

// Admin-facing account view. Role is the effective role; RolePinned marks the accounts whose
// role is pinned by email and cannot be changed or deleted from the panel. Sensitive fields
// (password hash, avatar object key) are intentionally absent.
public sealed record AdminUserResponse(
    string Id,
    string? Email,
    string Username,
    UserGender? Gender,
    UserRole Role,
    bool RolePinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? EmailVerifiedAt,
    int GarmentCount,
    int OutfitCount,
    int TryOnJobCount,
    int BodyReferencePhotoCount,
    int ActiveSessionCount,
    string? AvatarUrl,
    // Raw AI-credit ledger balance; null for accounts with unlimited credits (Admin).
    int? CreditBalance = null,
    // Read-only billing visibility; null when the account never subscribed.
    string? SubscriptionStatus = null,
    DateTimeOffset? SubscriptionPeriodEnd = null);

public sealed record AdminUsersPageResponse(
    IReadOnlyList<AdminUserResponse> Items,
    int TotalCount,
    int Offset,
    int Limit);

public sealed record AdminStatsResponse(
    int TotalUsers,
    int TotalGarments,
    int TotalOutfits,
    int TotalTryOnJobs);

public sealed record BillingSubscriptionResponse(string Status, DateTimeOffset? CurrentPeriodEnd, bool PremiumActive);

public sealed record BillingTopUpPackResponse(string Id, int Credits, string? DisplayPrice);

// Billing surface for the account: disabled billing keeps Enabled=false and empty packs
// so the UI degrades to the ask-the-admin notice.
public sealed record BillingStatusResponse(
    bool Enabled,
    string Provider,
    bool SubscriptionPriceConfigured,
    string? PremiumDisplayPrice,
    BillingSubscriptionResponse? Subscription,
    IReadOnlyList<BillingTopUpPackResponse> TopUpPacks,
    bool PortalAvailable);

public sealed record BillingCheckoutResponse(string Url);

public sealed record StartTopUpCheckoutRequest(string PackId);

public sealed record AuthSessionResponse(AuthUserResponse User, DateTimeOffset ExpiresAt);

public sealed record AuthProviderResponse(string Id, string Label, bool Configured, string Flow);
