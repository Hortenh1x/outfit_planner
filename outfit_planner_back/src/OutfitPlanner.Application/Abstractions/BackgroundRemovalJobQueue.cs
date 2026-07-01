namespace OutfitPlanner.Application.Abstractions;

/// <summary>
/// Queue of background-removal job ids. In-memory when Redis is not configured; a Redis list
/// otherwise. Mirrors <c>ITryOnJobQueue</c>.
/// </summary>
public interface IBackgroundRemovalJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Guid> DequeueAsync(CancellationToken cancellationToken);
}
