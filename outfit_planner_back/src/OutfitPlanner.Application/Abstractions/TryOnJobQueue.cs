namespace OutfitPlanner.Application.Abstractions;

public interface ITryOnJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Guid> DequeueAsync(CancellationToken cancellationToken);
}
