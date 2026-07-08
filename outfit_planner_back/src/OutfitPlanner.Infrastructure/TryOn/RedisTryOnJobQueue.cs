using OutfitPlanner.Application.Abstractions;
using StackExchange.Redis;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed class RedisTryOnJobQueue : ITryOnJobQueue
{
    public const string DefaultQueueName = "outfit-planner:try-on-jobs";
    public const string PriorityQueueSuffix = ":priority";

    private readonly IDatabase _database;
    private readonly RedisKey _queueName;
    private readonly RedisKey _priorityQueueName;
    private readonly TimeSpan _pollInterval;

    public RedisTryOnJobQueue(IConnectionMultiplexer connection, string queueName = DefaultQueueName, TimeSpan? pollInterval = null)
    {
        _database = connection.GetDatabase();
        _queueName = queueName;
        _priorityQueueName = queueName + PriorityQueueSuffix;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public async ValueTask EnqueueAsync(Guid jobId, bool priority = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ListRightPushAsync(priority ? _priorityQueueName : _queueName, jobId.ToString("D"));
    }

    public async Task<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Premium list first, then the normal list.
            var value = await _database.ListLeftPopAsync(_priorityQueueName);
            if (!value.HasValue)
            {
                value = await _database.ListLeftPopAsync(_queueName);
            }

            if (value.HasValue && Guid.TryParse(value.ToString(), out var jobId))
            {
                return jobId;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }
}
