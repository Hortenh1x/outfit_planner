using OutfitPlanner.Application.Abstractions;
using StackExchange.Redis;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed class RedisTryOnJobQueue : ITryOnJobQueue
{
    public const string DefaultQueueName = "outfit-planner:try-on-jobs";

    private readonly IDatabase _database;
    private readonly RedisKey _queueName;
    private readonly TimeSpan _pollInterval;

    public RedisTryOnJobQueue(IConnectionMultiplexer connection, string queueName = DefaultQueueName, TimeSpan? pollInterval = null)
    {
        _database = connection.GetDatabase();
        _queueName = queueName;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ListRightPushAsync(_queueName, jobId.ToString("D"));
    }

    public async Task<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var value = await _database.ListLeftPopAsync(_queueName);
            if (value.HasValue && Guid.TryParse(value.ToString(), out var jobId))
            {
                return jobId;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }
}
