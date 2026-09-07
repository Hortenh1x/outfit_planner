using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;

namespace OutfitPlanner.Api;

public sealed class TryOnBackgroundWorker : BackgroundService
{
    private readonly ITryOnJobQueue _queue;
    private readonly TryOnService _tryOn;
    private readonly ILogger<TryOnBackgroundWorker> _logger;

    public TryOnBackgroundWorker(ITryOnJobQueue queue, TryOnService tryOn, ILogger<TryOnBackgroundWorker> logger)
    {
        _queue = queue;
        _tryOn = tryOn;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);
                var result = await _tryOn.ProcessQueuedJobAsync(jobId, stoppingToken);
                if (result.Status == Domain.TryOnStatus.Failed)
                {
                    // The job row only carries the user-facing text; the provider detail lives here.
                    _logger.LogWarning("Try-on job {JobId} failed: {Detail}", jobId, result.FailureDetail ?? "no detail");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Try-on background worker failed while processing a queued job.");
            }
        }
    }
}
