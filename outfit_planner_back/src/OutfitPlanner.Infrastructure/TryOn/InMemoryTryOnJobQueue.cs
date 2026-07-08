using System.Threading.Channels;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed class InMemoryTryOnJobQueue : ITryOnJobQueue
{
    private readonly Channel<Guid> _priorityQueue = Channel.CreateUnbounded<Guid>();
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

    public async ValueTask EnqueueAsync(Guid jobId, bool priority = false, CancellationToken cancellationToken = default)
    {
        var target = priority ? _priorityQueue : _queue;
        await target.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async Task<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            // Drain premium work first; fall back to the normal queue, then wait for either.
            if (_priorityQueue.Reader.TryRead(out var priorityJobId))
            {
                return priorityJobId;
            }

            if (_queue.Reader.TryRead(out var jobId))
            {
                return jobId;
            }

            var priorityWait = _priorityQueue.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var normalWait = _queue.Reader.WaitToReadAsync(cancellationToken).AsTask();
            await Task.WhenAny(priorityWait, normalWait);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
