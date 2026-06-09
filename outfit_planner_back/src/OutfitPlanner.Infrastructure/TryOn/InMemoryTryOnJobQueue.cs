using System.Threading.Channels;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed class InMemoryTryOnJobQueue : ITryOnJobQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async Task<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
