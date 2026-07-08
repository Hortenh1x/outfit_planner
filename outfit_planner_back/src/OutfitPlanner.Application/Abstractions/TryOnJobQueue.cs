namespace OutfitPlanner.Application.Abstractions;

public interface ITryOnJobQueue
{
    // Priority entries (premium/admin accounts) are dequeued before normal ones.
    ValueTask EnqueueAsync(Guid jobId, bool priority = false, CancellationToken cancellationToken = default);
    Task<Guid> DequeueAsync(CancellationToken cancellationToken);
}
