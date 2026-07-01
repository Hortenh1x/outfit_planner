using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.BackgroundRemoval;

public sealed class InMemoryBackgroundRemovalJobRepository : IBackgroundRemovalJobRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, BackgroundRemovalJob> _jobs = new();

    public void AddBackgroundRemovalJob(BackgroundRemovalJob job)
    {
        lock (_lock)
        {
            _jobs[job.Id] = job;
        }
    }

    public BackgroundRemovalJob? GetBackgroundRemovalJobById(Guid jobId)
    {
        lock (_lock)
        {
            return _jobs.GetValueOrDefault(jobId);
        }
    }

    public BackgroundRemovalJob? GetBackgroundRemovalJobByUser(string userId, Guid jobId)
    {
        lock (_lock)
        {
            return _jobs.TryGetValue(jobId, out var job) && job.UserId == userId ? job : null;
        }
    }

    public void UpdateBackgroundRemovalJob(BackgroundRemovalJob job)
    {
        lock (_lock)
        {
            _jobs[job.Id] = job;
        }
    }

    public IReadOnlyList<BackgroundRemovalJob> ListUnfinishedBackgroundRemovalJobs()
    {
        lock (_lock)
        {
            return _jobs.Values
                .Where(job => job.Status is BackgroundRemovalStatus.Pending or BackgroundRemovalStatus.Processing)
                .ToList();
        }
    }
}
