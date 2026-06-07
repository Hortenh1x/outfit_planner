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
    IShareLinkRepository
{
    private readonly object _lock = new();
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
        lock (_lock)
        {
            return _garments.Values
                .Where(garment => garment.UserId == userId)
                .OrderBy(garment => garment.Category)
                .ThenBy(garment => garment.Name)
                .ToList();
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
        lock (_lock)
        {
            return _outfits.Values
                .Where(outfit => outfit.UserId == userId)
                .OrderByDescending(outfit => outfit.CreatedAt)
                .ToList();
        }
    }

    public void UpdateOutfit(Outfit outfit)
    {
        lock (_lock)
        {
            _outfits[outfit.Id] = outfit;
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
}
