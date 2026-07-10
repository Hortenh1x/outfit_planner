using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public interface IBodyReferencePhotoRepository
{
    void AddBodyReferencePhoto(BodyReferencePhoto photo);
    BodyReferencePhoto? GetBodyReferencePhotoByUser(string userId, Guid photoId);
    IReadOnlyList<BodyReferencePhoto> ListBodyReferencePhotosByUser(string userId);
    bool DeleteBodyReferencePhotoByUser(string userId, Guid photoId);
}

public interface IGarmentRepository
{
    void AddGarment(GarmentItem garment);
    GarmentItem? GetGarmentByUser(string userId, Guid garmentId);
    IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId);
    IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId, GarmentQuery query);
    // Garments (across all users) that have no stored perceptual hash yet, capped at `limit`.
    IReadOnlyList<GarmentItem> ListGarmentsMissingPerceptualHash(int limit);
    // Garments (across all users) whose cutout exists (background removal succeeded) but has no
    // stored cutout measurement yet, capped at `limit`.
    IReadOnlyList<GarmentItem> ListGarmentsMissingCutoutMeasurement(int limit);
    void UpdateGarment(GarmentItem garment);
    // Column-scoped updates for the startup backfill workers: both backfills can touch the same
    // row concurrently, so they must not rewrite the whole record from a stale in-memory copy.
    void UpdateGarmentPerceptualHash(Guid garmentId, string perceptualHash);
    void UpdateGarmentCutoutMeasurement(Guid garmentId, int cutoutWidthPx, int cutoutHeightPx);
    bool DeleteGarmentByUser(string userId, Guid garmentId);
}

public interface IOutfitRepository
{
    void AddOutfit(Outfit outfit);
    Outfit? GetOutfitByUser(string userId, Guid outfitId);
    Outfit? GetOutfitById(Guid outfitId);
    IReadOnlyList<Outfit> ListOutfitsByUser(string userId);
    IReadOnlyList<Outfit> ListOutfitsByUser(string userId, OutfitQuery query);
    void UpdateOutfit(Outfit outfit);
    bool DeleteOutfitByUser(string userId, Guid outfitId);
}

public interface IOutfitScheduleRepository
{
    void UpsertScheduledOutfit(ScheduledOutfit scheduled);
    IReadOnlyList<ScheduledOutfit> ListScheduleByUser(string userId, DateOnly from, DateOnly to);
    bool DeleteScheduledOutfitByUserDate(string userId, DateOnly date);
}

public interface ITryOnJobRepository
{
    void AddTryOnJob(TryOnJob job);
    TryOnJob? GetTryOnJobByUser(string userId, Guid jobId);
    TryOnJob? GetTryOnJobById(Guid jobId);
    TryOnJob? FindSucceededTryOnJobByCacheKey(string userId, string cacheKey);
    IReadOnlyList<TryOnJob> ListTryOnJobsByUser(string userId);
    void UpdateTryOnJob(TryOnJob job);
}

public interface IShareLinkRepository
{
    void AddShareLink(ShareLink link);
    ShareLink? GetActiveShareLink(string token);
    bool RevokeShareLinkByUser(string userId, string token, DateTimeOffset revokedAt);
}

// AI-credit ledger reads/writes (paywall metering). The balance is the sum of non-expired
// entries; reason-based lookups back idempotent grants and refunds.
public interface ICreditLedgerRepository
{
    void AddCreditEntry(CreditLedgerEntry entry);
    IReadOnlyList<CreditLedgerEntry> ListCreditEntriesByUser(string userId);
    int GetCreditBalance(string userId, DateTimeOffset now);
    bool HasCreditEntryWithReasonSince(string userId, CreditLedgerReason reason, DateTimeOffset since);
    // Sum of all deltas with the given reason; backs the top-up-to-config trial grant.
    int GetCreditSumByReason(string userId, CreditLedgerReason reason);
    IReadOnlyList<CreditLedgerEntry> ListCreditEntriesByJob(Guid tryOnJobId);
}

public sealed record AdminUserQuery(string? Search, UserRole? Role, int Offset, int Limit);

public sealed record AdminUserRecord(
    UserAccount User,
    int GarmentCount,
    int OutfitCount,
    int TryOnJobCount,
    int BodyReferencePhotoCount,
    int ActiveSessionCount,
    // Read-only billing visibility for the admin panel; null when never subscribed.
    string? SubscriptionStatus = null,
    DateTimeOffset? SubscriptionPeriodEnd = null);

public sealed record AdminUserStats(int TotalUsers, int TotalGarments, int TotalOutfits, int TotalTryOnJobs);

// Cross-user reads for the admin panel. Search matches email or display name; the role
// filter matches the stored role (pinned accounts converge on sign-in, so stored equals
// effective in practice).
public interface IAdminUserRepository
{
    IReadOnlyList<AdminUserRecord> ListUsers(AdminUserQuery query, DateTimeOffset now);
    int CountUsers(AdminUserQuery query);
    AdminUserRecord? GetUserRecord(string userId, DateTimeOffset now);
    AdminUserStats GetStats();
}

public interface IUserAccountRepository
{
    void AddUser(UserAccount user);
    void UpdateUser(UserAccount user);
    UserAccount? GetUserById(string userId);
    UserAccount? GetUserByNormalizedEmail(string normalizedEmail);
    void AddExternalLogin(ExternalAuthLogin login);
    ExternalAuthLogin? GetExternalLogin(string provider, string providerSubject);
    void UpdateExternalLogin(ExternalAuthLogin login);
    void AddAuthSession(AuthSession session);
    AuthSession? GetActiveAuthSessionByTokenHash(string tokenHash, DateTimeOffset now);
    IReadOnlyList<AuthSession> ListAuthSessionsByUser(string userId, DateTimeOffset now);
    void RevokeAuthSessionByTokenHash(string tokenHash, DateTimeOffset revokedAt);
    void RevokeAuthSessionsByUser(string userId, DateTimeOffset revokedAt);
    int DeleteExpiredAuthSessions(DateTimeOffset now);
    void AddEmailVerificationToken(AuthEmailVerificationToken token);
    AuthEmailVerificationToken? GetActiveEmailVerificationToken(string tokenHash, DateTimeOffset now);
    void MarkEmailVerificationTokenUsed(string tokenHash, DateTimeOffset usedAt);
    void AddPasswordResetToken(AuthPasswordResetToken token);
    AuthPasswordResetToken? GetActivePasswordResetToken(string tokenHash, DateTimeOffset now);
    void MarkPasswordResetTokenUsed(string tokenHash, DateTimeOffset usedAt);
    bool DeleteUserById(string userId);
}
