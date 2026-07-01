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
    void UpdateGarment(GarmentItem garment);
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
