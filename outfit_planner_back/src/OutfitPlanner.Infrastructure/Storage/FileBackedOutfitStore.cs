using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class FileBackedOutfitStore :
    IBodyReferencePhotoRepository,
    IGarmentRepository,
    IOutfitRepository,
    IOutfitScheduleRepository,
    ITryOnJobRepository,
    IShareLinkRepository,
    IUserAccountRepository,
    IAdminUserRepository,
    ICreditLedgerRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _writeLock = new();
    private readonly string _snapshotPath;
    private readonly InMemoryOutfitStore _inner;

    public FileBackedOutfitStore(string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            throw new ArgumentException("Local store snapshot path is required.", nameof(snapshotPath));
        }

        _snapshotPath = Path.GetFullPath(snapshotPath);
        _inner = new InMemoryOutfitStore(LoadSnapshot(_snapshotPath));
    }

    public GarmentItem CreateGarment(CreateGarmentCommand command)
    {
        return Mutate(() => _inner.CreateGarment(command));
    }

    public void AddBodyReferencePhoto(BodyReferencePhoto photo)
    {
        Mutate(() => _inner.AddBodyReferencePhoto(photo));
    }

    public IReadOnlyList<BodyReferencePhoto> ListBodyReferencePhotosByUser(string userId)
    {
        return _inner.ListBodyReferencePhotosByUser(userId);
    }

    public BodyReferencePhoto? GetBodyReferencePhotoByUser(string userId, Guid photoId)
    {
        return _inner.GetBodyReferencePhotoByUser(userId, photoId);
    }

    public bool DeleteBodyReferencePhotoByUser(string userId, Guid photoId)
    {
        return MutateIfChanged(() => _inner.DeleteBodyReferencePhotoByUser(userId, photoId));
    }

    public void AddGarment(GarmentItem garment)
    {
        Mutate(() => _inner.AddGarment(garment));
    }

    public GarmentItem? GetGarmentByUser(string userId, Guid garmentId)
    {
        return _inner.GetGarmentByUser(userId, garmentId);
    }

    public IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId)
    {
        return _inner.ListGarmentsByUser(userId);
    }

    public IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId, GarmentQuery query)
    {
        return _inner.ListGarmentsByUser(userId, query);
    }

    public IReadOnlyList<GarmentItem> ListGarmentsMissingPerceptualHash(int limit)
    {
        return _inner.ListGarmentsMissingPerceptualHash(limit);
    }

    public IReadOnlyList<GarmentItem> ListGarmentsMissingCutoutMeasurement(int limit)
    {
        return _inner.ListGarmentsMissingCutoutMeasurement(limit);
    }

    public void UpdateGarment(GarmentItem garment)
    {
        Mutate(() => _inner.UpdateGarment(garment));
    }

    public void UpdateGarmentPerceptualHash(Guid garmentId, string perceptualHash)
    {
        Mutate(() => _inner.UpdateGarmentPerceptualHash(garmentId, perceptualHash));
    }

    public void UpdateGarmentCutoutMeasurement(Guid garmentId, int cutoutWidthPx, int cutoutHeightPx)
    {
        Mutate(() => _inner.UpdateGarmentCutoutMeasurement(garmentId, cutoutWidthPx, cutoutHeightPx));
    }

    public bool DeleteGarmentByUser(string userId, Guid garmentId)
    {
        return MutateIfChanged(() => _inner.DeleteGarmentByUser(userId, garmentId));
    }

    public void AddOutfit(Outfit outfit)
    {
        Mutate(() => _inner.AddOutfit(outfit));
    }

    public Outfit? GetOutfitByUser(string userId, Guid outfitId)
    {
        return _inner.GetOutfitByUser(userId, outfitId);
    }

    public Outfit? GetOutfitById(Guid outfitId)
    {
        return _inner.GetOutfitById(outfitId);
    }

    public IReadOnlyList<Outfit> ListOutfitsByUser(string userId)
    {
        return _inner.ListOutfitsByUser(userId);
    }

    public IReadOnlyList<Outfit> ListOutfitsByUser(string userId, OutfitQuery query)
    {
        return _inner.ListOutfitsByUser(userId, query);
    }

    public void UpdateOutfit(Outfit outfit)
    {
        Mutate(() => _inner.UpdateOutfit(outfit));
    }

    public bool DeleteOutfitByUser(string userId, Guid outfitId)
    {
        return MutateIfChanged(() => _inner.DeleteOutfitByUser(userId, outfitId));
    }

    public void UpsertScheduledOutfit(ScheduledOutfit scheduled)
    {
        Mutate(() => _inner.UpsertScheduledOutfit(scheduled));
    }

    public IReadOnlyList<ScheduledOutfit> ListScheduleByUser(string userId, DateOnly from, DateOnly to)
    {
        return _inner.ListScheduleByUser(userId, from, to);
    }

    public bool DeleteScheduledOutfitByUserDate(string userId, DateOnly date)
    {
        return MutateIfChanged(() => _inner.DeleteScheduledOutfitByUserDate(userId, date));
    }

    public void AddTryOnJob(TryOnJob job)
    {
        Mutate(() => _inner.AddTryOnJob(job));
    }

    public TryOnJob? GetTryOnJobByUser(string userId, Guid jobId)
    {
        return _inner.GetTryOnJobByUser(userId, jobId);
    }

    public TryOnJob? GetTryOnJobById(Guid jobId)
    {
        return _inner.GetTryOnJobById(jobId);
    }

    public TryOnJob? FindSucceededTryOnJobByCacheKey(string userId, string cacheKey)
    {
        return _inner.FindSucceededTryOnJobByCacheKey(userId, cacheKey);
    }

    public IReadOnlyList<TryOnJob> ListTryOnJobsByUser(string userId)
    {
        return _inner.ListTryOnJobsByUser(userId);
    }

    public void UpdateTryOnJob(TryOnJob job)
    {
        Mutate(() => _inner.UpdateTryOnJob(job));
    }

    public void AddShareLink(ShareLink link)
    {
        Mutate(() => _inner.AddShareLink(link));
    }

    public ShareLink? GetActiveShareLink(string token)
    {
        return _inner.GetActiveShareLink(token);
    }

    public bool RevokeShareLinkByUser(string userId, string token, DateTimeOffset revokedAt)
    {
        return MutateIfChanged(() => _inner.RevokeShareLinkByUser(userId, token, revokedAt));
    }

    public void AddUser(UserAccount user)
    {
        Mutate(() => _inner.AddUser(user));
    }

    public void UpdateUser(UserAccount user)
    {
        Mutate(() => _inner.UpdateUser(user));
    }

    public UserAccount? GetUserById(string userId)
    {
        return _inner.GetUserById(userId);
    }

    public UserAccount? GetUserByNormalizedEmail(string normalizedEmail)
    {
        return _inner.GetUserByNormalizedEmail(normalizedEmail);
    }

    public void AddExternalLogin(ExternalAuthLogin login)
    {
        Mutate(() => _inner.AddExternalLogin(login));
    }

    public ExternalAuthLogin? GetExternalLogin(string provider, string providerSubject)
    {
        return _inner.GetExternalLogin(provider, providerSubject);
    }

    public void UpdateExternalLogin(ExternalAuthLogin login)
    {
        Mutate(() => _inner.UpdateExternalLogin(login));
    }

    public void AddAuthSession(AuthSession session)
    {
        Mutate(() => _inner.AddAuthSession(session));
    }

    public AuthSession? GetActiveAuthSessionByTokenHash(string tokenHash, DateTimeOffset now)
    {
        return _inner.GetActiveAuthSessionByTokenHash(tokenHash, now);
    }

    public void RevokeAuthSessionByTokenHash(string tokenHash, DateTimeOffset revokedAt)
    {
        Mutate(() => _inner.RevokeAuthSessionByTokenHash(tokenHash, revokedAt));
    }

    public IReadOnlyList<AuthSession> ListAuthSessionsByUser(string userId, DateTimeOffset now)
    {
        return _inner.ListAuthSessionsByUser(userId, now);
    }

    public void RevokeAuthSessionsByUser(string userId, DateTimeOffset revokedAt)
    {
        Mutate(() => _inner.RevokeAuthSessionsByUser(userId, revokedAt));
    }

    public int DeleteExpiredAuthSessions(DateTimeOffset now)
    {
        return MutateCount(() => _inner.DeleteExpiredAuthSessions(now));
    }

    public void AddEmailVerificationToken(AuthEmailVerificationToken token)
    {
        Mutate(() => _inner.AddEmailVerificationToken(token));
    }

    public AuthEmailVerificationToken? GetActiveEmailVerificationToken(string tokenHash, DateTimeOffset now)
    {
        return _inner.GetActiveEmailVerificationToken(tokenHash, now);
    }

    public void MarkEmailVerificationTokenUsed(string tokenHash, DateTimeOffset usedAt)
    {
        Mutate(() => _inner.MarkEmailVerificationTokenUsed(tokenHash, usedAt));
    }

    public void AddPasswordResetToken(AuthPasswordResetToken token)
    {
        Mutate(() => _inner.AddPasswordResetToken(token));
    }

    public AuthPasswordResetToken? GetActivePasswordResetToken(string tokenHash, DateTimeOffset now)
    {
        return _inner.GetActivePasswordResetToken(tokenHash, now);
    }

    public void MarkPasswordResetTokenUsed(string tokenHash, DateTimeOffset usedAt)
    {
        Mutate(() => _inner.MarkPasswordResetTokenUsed(tokenHash, usedAt));
    }

    public bool DeleteUserById(string userId)
    {
        return MutateIfChanged(() => _inner.DeleteUserById(userId));
    }

    public IReadOnlyList<AdminUserRecord> ListUsers(AdminUserQuery query, DateTimeOffset now)
    {
        return _inner.ListUsers(query, now);
    }

    public int CountUsers(AdminUserQuery query)
    {
        return _inner.CountUsers(query);
    }

    public AdminUserRecord? GetUserRecord(string userId, DateTimeOffset now)
    {
        return _inner.GetUserRecord(userId, now);
    }

    public AdminUserStats GetStats()
    {
        return _inner.GetStats();
    }

    public void AddCreditEntry(CreditLedgerEntry entry)
    {
        Mutate(() => _inner.AddCreditEntry(entry));
    }

    public IReadOnlyList<CreditLedgerEntry> ListCreditEntriesByUser(string userId)
    {
        return _inner.ListCreditEntriesByUser(userId);
    }

    public int GetCreditBalance(string userId, DateTimeOffset now)
    {
        return _inner.GetCreditBalance(userId, now);
    }

    public bool HasCreditEntryWithReasonSince(string userId, CreditLedgerReason reason, DateTimeOffset since)
    {
        return _inner.HasCreditEntryWithReasonSince(userId, reason, since);
    }

    public int GetCreditSumByReason(string userId, CreditLedgerReason reason)
    {
        return _inner.GetCreditSumByReason(userId, reason);
    }

    public IReadOnlyList<CreditLedgerEntry> ListCreditEntriesByJob(Guid tryOnJobId)
    {
        return _inner.ListCreditEntriesByJob(tryOnJobId);
    }

    private void Mutate(Action action)
    {
        lock (_writeLock)
        {
            action();
            SaveSnapshot();
        }
    }

    private T Mutate<T>(Func<T> action)
    {
        lock (_writeLock)
        {
            var result = action();
            SaveSnapshot();
            return result;
        }
    }

    private bool MutateIfChanged(Func<bool> action)
    {
        lock (_writeLock)
        {
            var changed = action();
            if (changed)
            {
                SaveSnapshot();
            }

            return changed;
        }
    }

    private int MutateCount(Func<int> action)
    {
        lock (_writeLock)
        {
            var count = action();
            if (count > 0)
            {
                SaveSnapshot();
            }

            return count;
        }
    }

    private void SaveSnapshot()
    {
        var directory = Path.GetDirectoryName(_snapshotPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Local store snapshot directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(_snapshotPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = JsonSerializer.Serialize(_inner.ExportSnapshot(), JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _snapshotPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static InMemoryOutfitStoreSnapshot LoadSnapshot(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
        {
            return InMemoryOutfitStoreSnapshot.Empty;
        }

        try
        {
            var json = RemoveRetiredHatEntries(File.ReadAllText(snapshotPath));
            return JsonSerializer.Deserialize<InMemoryOutfitStoreSnapshot>(json, JsonOptions)
                ?? InMemoryOutfitStoreSnapshot.Empty;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Local outfit store snapshot is not valid JSON: {snapshotPath}", ex);
        }
    }

    // The Hat garment category was retired (hats were replaced by global hairstyle presets), but
    // older local snapshots may still carry Hat records, and the string enum converter would
    // fail on the removed value. Drop those garments and outfit items before deserializing —
    // the same purge the Postgres migration applies.
    private static string RemoveRetiredHatEntries(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Not parseable as a document; let the typed deserialization report the error.
            return json;
        }

        if (root is not JsonObject snapshot)
        {
            return json;
        }

        var changed = false;
        if (snapshot["Garments"] is JsonArray garments)
        {
            changed |= RemoveHatCategoryEntries(garments);
        }

        if (snapshot["Outfits"] is JsonArray outfits)
        {
            foreach (var outfit in outfits)
            {
                if (outfit?["Items"] is JsonArray items)
                {
                    changed |= RemoveHatCategoryEntries(items);
                }
            }
        }

        return changed ? root.ToJsonString(JsonOptions) : json;
    }

    private static bool RemoveHatCategoryEntries(JsonArray entries)
    {
        var removed = false;
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index] is JsonObject entry
                && entry["Category"] is JsonValue category
                && category.TryGetValue<string>(out var value)
                && string.Equals(value, "Hat", StringComparison.Ordinal))
            {
                entries.RemoveAt(index);
                removed = true;
            }
        }

        return removed;
    }
}
