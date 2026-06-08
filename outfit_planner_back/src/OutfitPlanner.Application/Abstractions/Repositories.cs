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
    bool DeleteGarmentByUser(string userId, Guid garmentId);
}

public interface IOutfitRepository
{
    void AddOutfit(Outfit outfit);
    Outfit? GetOutfitByUser(string userId, Guid outfitId);
    Outfit? GetOutfitById(Guid outfitId);
    IReadOnlyList<Outfit> ListOutfitsByUser(string userId);
    void UpdateOutfit(Outfit outfit);
}

public interface IOutfitScheduleRepository
{
    void UpsertScheduledOutfit(ScheduledOutfit scheduled);
    IReadOnlyList<ScheduledOutfit> ListScheduleByUser(string userId, DateOnly from, DateOnly to);
}

public interface ITryOnJobRepository
{
    void AddTryOnJob(TryOnJob job);
    TryOnJob? GetTryOnJobByUser(string userId, Guid jobId);
    void UpdateTryOnJob(TryOnJob job);
}

public interface IShareLinkRepository
{
    void AddShareLink(ShareLink link);
    ShareLink? GetActiveShareLink(string token);
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
    void RevokeAuthSessionByTokenHash(string tokenHash, DateTimeOffset revokedAt);
}
