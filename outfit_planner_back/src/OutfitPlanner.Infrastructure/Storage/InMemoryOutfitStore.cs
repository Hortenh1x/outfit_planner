using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class InMemoryOutfitStore :
    IBodyReferencePhotoRepository,
    IGarmentRepository,
    IOutfitRepository,
    IOutfitScheduleRepository,
    ITryOnJobRepository,
    IShareLinkRepository,
    IUserAccountRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<string, UserAccount> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExternalAuthLogin> _externalLogins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AuthSession> _authSessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AuthEmailVerificationToken> _emailVerificationTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AuthPasswordResetToken> _passwordResetTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, BodyReferencePhoto> _bodyPhotos = new();
    private readonly Dictionary<Guid, GarmentItem> _garments = new();
    private readonly Dictionary<Guid, Outfit> _outfits = new();
    private readonly Dictionary<(string UserId, DateOnly Date), ScheduledOutfit> _schedule = new();
    private readonly Dictionary<Guid, TryOnJob> _tryOnJobs = new();
    private readonly Dictionary<string, ShareLink> _shareLinks = new(StringComparer.OrdinalIgnoreCase);

    public GarmentItem CreateGarment(CreateGarmentCommand command)
    {
        var imageUrl = InputGuard.RequireText(command.ImageUrl, "Garment image URL");
        var garment = new GarmentItem(
            Guid.NewGuid(),
            InputGuard.NormalizeUserId(command.UserId),
            InputGuard.RequireText(command.Name, "Garment name"),
            command.Category,
            GarmentRules.GetBodyZone(command.Category),
            imageUrl,
            string.IsNullOrWhiteSpace(command.ThumbnailUrl) ? imageUrl : command.ThumbnailUrl.Trim(),
            command.Tags,
            command.PrimaryColor,
            command.SecondaryColors ?? Array.Empty<string>(),
            command.Material,
            command.Brand,
            command.Size,
            command.Season ?? Array.Empty<string>(),
            command.WeatherMinTemp,
            command.WeatherMaxTemp,
            command.Occasion ?? Array.Empty<string>(),
            command.FormalityScore,
            command.WarmthScore,
            command.ComfortScore,
            command.IsFavorite,
            command.IsArchived,
            command.LastWornAt,
            command.LaundryStatus ?? "clean",
            DateTimeOffset.UtcNow);

        AddGarment(garment);
        return garment;
    }

    public void AddBodyReferencePhoto(BodyReferencePhoto photo)
    {
        lock (_lock)
        {
            _bodyPhotos[photo.Id] = photo;
        }
    }

    public IReadOnlyList<BodyReferencePhoto> ListBodyReferencePhotosByUser(string userId)
    {
        lock (_lock)
        {
            return _bodyPhotos.Values
                .Where(photo => photo.UserId == userId)
                .OrderByDescending(photo => photo.CreatedAt)
                .ToList();
        }
    }

    public BodyReferencePhoto? GetBodyReferencePhotoByUser(string userId, Guid photoId)
    {
        lock (_lock)
        {
            return _bodyPhotos.TryGetValue(photoId, out var photo) && photo.UserId == userId
                ? photo
                : null;
        }
    }

    public bool DeleteBodyReferencePhotoByUser(string userId, Guid photoId)
    {
        lock (_lock)
        {
            return _bodyPhotos.TryGetValue(photoId, out var photo)
                && photo.UserId == userId
                && _bodyPhotos.Remove(photoId);
        }
    }

    public void AddGarment(GarmentItem garment)
    {
        lock (_lock)
        {
            _garments[garment.Id] = garment;
        }
    }

    public GarmentItem? GetGarmentByUser(string userId, Guid garmentId)
    {
        lock (_lock)
        {
            return _garments.TryGetValue(garmentId, out var garment) && garment.UserId == userId
                ? garment
                : null;
        }
    }

    public IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId)
    {
        return ListGarmentsByUser(userId, new GarmentQuery());
    }

    public IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId, GarmentQuery query)
    {
        lock (_lock)
        {
            var garments = _garments.Values
                .Where(garment => garment.UserId == userId)
                .Where(garment => MatchesGarmentQuery(garment, query));

            garments = SortGarments(garments, query.Sort);

            if (query.Offset is { } offset)
            {
                garments = garments.Skip(offset);
            }

            if (query.Limit is { } limit)
            {
                garments = garments.Take(limit);
            }

            return garments.ToList();
        }
    }

    public void UpdateGarment(GarmentItem garment)
    {
        lock (_lock)
        {
            _garments[garment.Id] = garment;
        }
    }

    public bool DeleteGarmentByUser(string userId, Guid garmentId)
    {
        lock (_lock)
        {
            return _garments.TryGetValue(garmentId, out var garment)
                && garment.UserId == userId
                && _garments.Remove(garmentId);
        }
    }

    public void AddOutfit(Outfit outfit)
    {
        lock (_lock)
        {
            _outfits[outfit.Id] = outfit;
        }
    }

    public Outfit? GetOutfitByUser(string userId, Guid outfitId)
    {
        lock (_lock)
        {
            return _outfits.TryGetValue(outfitId, out var outfit) && outfit.UserId == userId
                ? outfit
                : null;
        }
    }

    public Outfit? GetOutfitById(Guid outfitId)
    {
        lock (_lock)
        {
            return _outfits.GetValueOrDefault(outfitId);
        }
    }

    public IReadOnlyList<Outfit> ListOutfitsByUser(string userId)
    {
        return ListOutfitsByUser(userId, new OutfitQuery());
    }

    public IReadOnlyList<Outfit> ListOutfitsByUser(string userId, OutfitQuery query)
    {
        lock (_lock)
        {
            var outfits = _outfits.Values
                .Where(outfit => outfit.UserId == userId)
                .Where(outfit => MatchesOutfitQuery(outfit, query));

            outfits = SortOutfits(outfits, query.Sort);

            if (query.Offset is { } offset)
            {
                outfits = outfits.Skip(offset);
            }

            if (query.Limit is { } limit)
            {
                outfits = outfits.Take(limit);
            }

            return outfits.ToList();
        }
    }

    public void UpdateOutfit(Outfit outfit)
    {
        lock (_lock)
        {
            _outfits[outfit.Id] = outfit;
        }
    }

    public bool DeleteOutfitByUser(string userId, Guid outfitId)
    {
        lock (_lock)
        {
            if (!_outfits.TryGetValue(outfitId, out var outfit) || outfit.UserId != userId)
            {
                return false;
            }

            _outfits.Remove(outfitId);
            foreach (var key in _schedule.Where(item => item.Value.UserId == userId && item.Value.OutfitId == outfitId).Select(item => item.Key).ToList())
            {
                _schedule.Remove(key);
            }

            foreach (var token in _shareLinks.Where(item => item.Value.UserId == userId && item.Value.OutfitId == outfitId).Select(item => item.Key).ToList())
            {
                _shareLinks.Remove(token);
            }

            foreach (var jobId in _tryOnJobs.Where(item => item.Value.UserId == userId && item.Value.OutfitId == outfitId).Select(item => item.Key).ToList())
            {
                _tryOnJobs.Remove(jobId);
            }

            return true;
        }
    }

    public void UpsertScheduledOutfit(ScheduledOutfit scheduled)
    {
        lock (_lock)
        {
            _schedule[(scheduled.UserId, scheduled.Date)] = scheduled;
        }
    }

    public IReadOnlyList<ScheduledOutfit> ListScheduleByUser(string userId, DateOnly from, DateOnly to)
    {
        lock (_lock)
        {
            return _schedule.Values
                .Where(item => item.UserId == userId && item.Date >= from && item.Date <= to)
                .OrderBy(item => item.Date)
                .ToList();
        }
    }

    public bool DeleteScheduledOutfitByUserDate(string userId, DateOnly date)
    {
        lock (_lock)
        {
            return _schedule.Remove((userId, date));
        }
    }

    public void AddTryOnJob(TryOnJob job)
    {
        lock (_lock)
        {
            _tryOnJobs[job.Id] = job;
        }
    }

    public TryOnJob? GetTryOnJobByUser(string userId, Guid jobId)
    {
        lock (_lock)
        {
            return _tryOnJobs.TryGetValue(jobId, out var job) && job.UserId == userId
                ? job
                : null;
        }
    }

    public TryOnJob? GetTryOnJobById(Guid jobId)
    {
        lock (_lock)
        {
            return _tryOnJobs.GetValueOrDefault(jobId);
        }
    }

    public TryOnJob? FindSucceededTryOnJobByCacheKey(string userId, string cacheKey)
    {
        lock (_lock)
        {
            return _tryOnJobs.Values
                .Where(job => job.UserId == userId)
                .Where(job => job.CacheKey == cacheKey)
                .Where(job => job.Status == TryOnStatus.Succeeded)
                .Where(job => !job.IsDeleted)
                .Where(job => !string.IsNullOrWhiteSpace(job.OutputImageUrl))
                .OrderByDescending(job => job.CreatedAt)
                .FirstOrDefault();
        }
    }

    public IReadOnlyList<TryOnJob> ListTryOnJobsByUser(string userId)
    {
        lock (_lock)
        {
            return _tryOnJobs.Values
                .Where(job => job.UserId == userId)
                .OrderByDescending(job => job.CreatedAt)
                .ToList();
        }
    }

    public void UpdateTryOnJob(TryOnJob job)
    {
        lock (_lock)
        {
            _tryOnJobs[job.Id] = job;
        }
    }

    public void AddShareLink(ShareLink link)
    {
        lock (_lock)
        {
            _shareLinks[link.Token] = link;
        }
    }

    public ShareLink? GetActiveShareLink(string token)
    {
        lock (_lock)
        {
            return _shareLinks.TryGetValue(token, out var link) && link.RevokedAt is null
                ? link
                : null;
        }
    }

    public bool RevokeShareLinkByUser(string userId, string token, DateTimeOffset revokedAt)
    {
        lock (_lock)
        {
            if (!_shareLinks.TryGetValue(token, out var link) || link.UserId != userId || link.RevokedAt is not null)
            {
                return false;
            }

            _shareLinks[token] = link with { RevokedAt = revokedAt };
            return true;
        }
    }

    public void AddUser(UserAccount user)
    {
        lock (_lock)
        {
            _users[user.Id] = user;
        }
    }

    public void UpdateUser(UserAccount user)
    {
        lock (_lock)
        {
            _users[user.Id] = user;
        }
    }

    public UserAccount? GetUserById(string userId)
    {
        lock (_lock)
        {
            return _users.GetValueOrDefault(userId);
        }
    }

    public UserAccount? GetUserByNormalizedEmail(string normalizedEmail)
    {
        lock (_lock)
        {
            return _users.Values.FirstOrDefault(user => string.Equals(user.NormalizedEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void AddExternalLogin(ExternalAuthLogin login)
    {
        lock (_lock)
        {
            _externalLogins[ExternalLoginKey(login.Provider, login.ProviderSubject)] = login;
        }
    }

    public ExternalAuthLogin? GetExternalLogin(string provider, string providerSubject)
    {
        lock (_lock)
        {
            return _externalLogins.GetValueOrDefault(ExternalLoginKey(provider, providerSubject));
        }
    }

    public void UpdateExternalLogin(ExternalAuthLogin login)
    {
        lock (_lock)
        {
            _externalLogins[ExternalLoginKey(login.Provider, login.ProviderSubject)] = login;
        }
    }

    public void AddAuthSession(AuthSession session)
    {
        lock (_lock)
        {
            _authSessions[session.TokenHash] = session;
        }
    }

    public AuthSession? GetActiveAuthSessionByTokenHash(string tokenHash, DateTimeOffset now)
    {
        lock (_lock)
        {
            return _authSessions.TryGetValue(tokenHash, out var session)
                && session.RevokedAt is null
                && session.ExpiresAt > now
                ? session
                : null;
        }
    }

    public void RevokeAuthSessionByTokenHash(string tokenHash, DateTimeOffset revokedAt)
    {
        lock (_lock)
        {
            if (_authSessions.TryGetValue(tokenHash, out var session))
            {
                _authSessions[tokenHash] = session with { RevokedAt = revokedAt };
            }
        }
    }

    public IReadOnlyList<AuthSession> ListAuthSessionsByUser(string userId, DateTimeOffset now)
    {
        lock (_lock)
        {
            return _authSessions.Values
                .Where(session => session.UserId == userId && session.ExpiresAt > now && session.RevokedAt is null)
                .OrderByDescending(session => session.CreatedAt)
                .ToList();
        }
    }

    public void RevokeAuthSessionsByUser(string userId, DateTimeOffset revokedAt)
    {
        lock (_lock)
        {
            foreach (var session in _authSessions.Values.Where(session => session.UserId == userId && session.RevokedAt is null).ToList())
            {
                _authSessions[session.TokenHash] = session with { RevokedAt = revokedAt };
            }
        }
    }

    public int DeleteExpiredAuthSessions(DateTimeOffset now)
    {
        lock (_lock)
        {
            var expiredKeys = _authSessions
                .Where(item => item.Value.ExpiresAt <= now)
                .Select(item => item.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _authSessions.Remove(key);
            }

            return expiredKeys.Count;
        }
    }

    public void AddEmailVerificationToken(AuthEmailVerificationToken token)
    {
        lock (_lock)
        {
            _emailVerificationTokens[token.TokenHash] = token;
        }
    }

    public AuthEmailVerificationToken? GetActiveEmailVerificationToken(string tokenHash, DateTimeOffset now)
    {
        lock (_lock)
        {
            return _emailVerificationTokens.TryGetValue(tokenHash, out var token)
                && token.UsedAt is null
                && token.ExpiresAt > now
                ? token
                : null;
        }
    }

    public void MarkEmailVerificationTokenUsed(string tokenHash, DateTimeOffset usedAt)
    {
        lock (_lock)
        {
            if (_emailVerificationTokens.TryGetValue(tokenHash, out var token))
            {
                _emailVerificationTokens[tokenHash] = token with { UsedAt = usedAt };
            }
        }
    }

    public void AddPasswordResetToken(AuthPasswordResetToken token)
    {
        lock (_lock)
        {
            _passwordResetTokens[token.TokenHash] = token;
        }
    }

    public AuthPasswordResetToken? GetActivePasswordResetToken(string tokenHash, DateTimeOffset now)
    {
        lock (_lock)
        {
            return _passwordResetTokens.TryGetValue(tokenHash, out var token)
                && token.UsedAt is null
                && token.ExpiresAt > now
                ? token
                : null;
        }
    }

    public void MarkPasswordResetTokenUsed(string tokenHash, DateTimeOffset usedAt)
    {
        lock (_lock)
        {
            if (_passwordResetTokens.TryGetValue(tokenHash, out var token))
            {
                _passwordResetTokens[tokenHash] = token with { UsedAt = usedAt };
            }
        }
    }

    public bool DeleteUserById(string userId)
    {
        lock (_lock)
        {
            if (!_users.Remove(userId))
            {
                return false;
            }

            foreach (var garmentId in _garments.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _garments.Remove(garmentId);
            }

            foreach (var outfitId in _outfits.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _outfits.Remove(outfitId);
            }

            foreach (var photoId in _bodyPhotos.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _bodyPhotos.Remove(photoId);
            }

            foreach (var jobId in _tryOnJobs.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _tryOnJobs.Remove(jobId);
            }

            foreach (var key in _schedule.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _schedule.Remove(key);
            }

            foreach (var token in _shareLinks.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _shareLinks.Remove(token);
            }

            foreach (var key in _authSessions.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _authSessions.Remove(key);
            }

            foreach (var key in _externalLogins.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _externalLogins.Remove(key);
            }

            foreach (var key in _emailVerificationTokens.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _emailVerificationTokens.Remove(key);
            }

            foreach (var key in _passwordResetTokens.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToList())
            {
                _passwordResetTokens.Remove(key);
            }

            return true;
        }
    }

    private static string ExternalLoginKey(string provider, string providerSubject)
    {
        return $"{provider.ToLowerInvariant()}:{providerSubject}";
    }

    private static bool MatchesGarmentQuery(GarmentItem garment, GarmentQuery query)
    {
        return (query.Category is null || garment.Category == query.Category)
            && (query.Color is null || string.Equals(garment.PrimaryColor, query.Color, StringComparison.OrdinalIgnoreCase) || garment.SecondaryColors.Contains(query.Color, StringComparer.OrdinalIgnoreCase))
            && (query.Season is null || garment.Season.Contains(query.Season, StringComparer.OrdinalIgnoreCase))
            && (query.Occasion is null || garment.Occasion.Contains(query.Occasion, StringComparer.OrdinalIgnoreCase))
            && (query.Favorite is null || garment.IsFavorite == query.Favorite)
            && (query.Archived is null || garment.IsArchived == query.Archived)
            && (query.Brand is null || ContainsText(garment.Brand, query.Brand))
            && (query.Material is null || ContainsText(garment.Material, query.Material))
            && (query.Search is null || MatchesGarmentSearch(garment, query.Search));
    }

    private static bool MatchesGarmentSearch(GarmentItem garment, string search)
    {
        return ContainsText(garment.Name, search)
            || ContainsText(garment.PrimaryColor, search)
            || ContainsText(garment.Material, search)
            || ContainsText(garment.Brand, search)
            || ContainsText(garment.Size, search)
            || garment.Tags.Any(tag => ContainsText(tag, search));
    }

    private static IEnumerable<GarmentItem> SortGarments(IEnumerable<GarmentItem> garments, string? sort)
    {
        return sort switch
        {
            "recent" => garments.OrderByDescending(garment => garment.CreatedAt),
            "oldest" => garments.OrderBy(garment => garment.CreatedAt),
            "name" => garments.OrderBy(garment => garment.Name),
            _ => garments.OrderBy(garment => garment.Category).ThenBy(garment => garment.Name)
        };
    }

    private static bool MatchesOutfitQuery(Outfit outfit, OutfitQuery query)
    {
        return (query.Occasion is null || outfit.Occasion.Contains(query.Occasion, StringComparer.OrdinalIgnoreCase))
            && (query.Favorite is null || outfit.IsFavorite == query.Favorite)
            && (query.Archived is null || outfit.IsArchived == query.Archived)
            && (query.Search is null || MatchesOutfitSearch(outfit, query.Search));
    }

    private static bool MatchesOutfitSearch(Outfit outfit, string search)
    {
        return ContainsText(outfit.Name, search)
            || outfit.Tags.Any(tag => ContainsText(tag, search))
            || outfit.Occasion.Any(occasion => ContainsText(occasion, search))
            || outfit.Items.Any(item => ContainsText(item.Name, search));
    }

    private static IEnumerable<Outfit> SortOutfits(IEnumerable<Outfit> outfits, string? sort)
    {
        return sort switch
        {
            "oldest" => outfits.OrderBy(outfit => outfit.CreatedAt),
            "name" => outfits.OrderBy(outfit => outfit.Name),
            _ => outfits.OrderByDescending(outfit => outfit.CreatedAt)
        };
    }

    private static bool ContainsText(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }
}
