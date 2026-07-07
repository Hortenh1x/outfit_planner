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
    string? PrimaryColor,
    IReadOnlyList<string> SecondaryColors,
    string? Material,
    string? Brand,
    string? Size,
    IReadOnlyList<string> Season,
    int? WeatherMinTemp,
    int? WeatherMaxTemp,
    IReadOnlyList<string> Occasion,
    int? FormalityScore,
    int? WarmthScore,
    int? ComfortScore,
    bool IsFavorite,
    bool IsArchived,
    DateTimeOffset? LastWornAt,
    string LaundryStatus,
    DateTimeOffset CreatedAt,
    double RotationDegrees = 0,
    // Average hash of the original photo BEFORE background removal, used to detect duplicate uploads.
    string? PerceptualHash = null,
    // Async background-removal state; Succeeded for legacy/already-processed garments.
    BackgroundRemovalStatus BackgroundRemovalStatus = BackgroundRemovalStatus.Succeeded,
    string? BackgroundRemovalError = null,
    // Alpha-bounding-box size of the current cutout in pixels. The absolute numbers depend on
    // the shot, but height/width is invariant to shooting distance and drives relative sizing.
    int? CutoutWidthPx = null,
    int? CutoutHeightPx = null);

public sealed record OutfitItem(
    Guid GarmentId,
    string Name,
    GarmentCategory Category,
    BodyZone BodyZone,
    string ThumbnailUrl,
    double RotationDegrees = 0,
    // Cutout alpha-bounding-box size of the garment, carried onto outfit items so composed
    // outfit rendering (cards, shared view) can reuse relative sizing without garment access.
    int? CutoutWidthPx = null,
    int? CutoutHeightPx = null);

public sealed record Outfit(
    Guid Id,
    string UserId,
    string Name,
    IReadOnlyList<OutfitItem> Items,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Occasion,
    bool IsFavorite,
    bool IsArchived,
    string? ClothesOnlyPreviewUrl,
    string? PersonPreviewUrl,
    DateTimeOffset CreatedAt)
{
    // Composed-figure state: the worn global hairstyle preset, its visibility, and the
    // silhouette gender the outfit was composed on. Null on legacy outfits (renderers fall
    // back to defaults).
    public string? HairstylePresetId { get; init; }
    public bool HairstyleVisible { get; init; } = true;
    public UserGender? SilhouetteGender { get; init; }
    // Response-only: resolved from the hairstyle catalog when building API responses; stores
    // never persist it (repositories always see it as null).
    public string? HairstyleAssetUrl { get; init; }
}

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
    bool SequentialFlowEnabled,
    TryOnStatus Status,
    string? ProviderJobId,
    string? OutputImageUrl,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public DateTimeOffset? ConsentAcceptedAt { get; init; }
    public string? ProviderName { get; init; }
    public string? ProviderRequestId { get; init; }
    public Guid? SourceBodyPhotoId { get; init; }
    public DateTimeOffset? RetentionUntil { get; init; }
    public bool IsDeleted { get; init; }
    public TryOnMode TryOnMode { get; init; } = TryOnMode.SequentialOutfitTryOn;
    public int ConfirmedCredits { get; init; }
    public string? CacheKey { get; init; }
    public bool ServedFromCache { get; init; }
    public Guid? SourceCachedJobId { get; init; }
    public string? ProviderSettingsHash { get; init; }
}

public sealed record ShareLink(
    string Token,
    string UserId,
    Guid OutfitId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record UserAccount(
    string Id,
    string? Email,
    string? NormalizedEmail,
    string DisplayName,
    string? PasswordHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLoginAt)
{
    public DateTimeOffset? EmailVerifiedAt { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public string? AvatarUrl { get; init; }
    public string? AvatarObjectKey { get; init; }
    public UserGender? Gender { get; init; }
    // Stored role. Pinned accounts (see the Application role-pinning policy) may override this
    // at read time; always resolve the effective role through the policy, not this field alone.
    public UserRole Role { get; init; } = UserRole.Free;
}

public sealed record AuthEmailVerificationToken(
    string TokenHash,
    string UserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UsedAt);

public sealed record AuthPasswordResetToken(
    string TokenHash,
    string UserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UsedAt);

public sealed record ExternalAuthLogin(
    string Provider,
    string ProviderSubject,
    string UserId,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt);

public sealed record AuthSession(
    Guid Id,
    string UserId,
    string TokenHash,
    string CsrfTokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);
