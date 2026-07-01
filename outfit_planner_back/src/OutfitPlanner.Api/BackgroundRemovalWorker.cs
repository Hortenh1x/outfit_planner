using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Api;

/// <summary>
/// Hosted worker that drains the background-removal queue and processes each job. Mirrors
/// <see cref="TryOnBackgroundWorker"/>.
/// </summary>
public sealed class BackgroundRemovalWorker : BackgroundService
{
    private readonly IBackgroundRemovalJobQueue _queue;
    private readonly IBackgroundRemovalJobProcessor _processor;
    private readonly ILogger<BackgroundRemovalWorker> _logger;

    public BackgroundRemovalWorker(IBackgroundRemovalJobQueue queue, IBackgroundRemovalJobProcessor processor, ILogger<BackgroundRemovalWorker> logger)
    {
        _queue = queue;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);
                await _processor.ProcessAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background removal worker failed while processing a queued job.");
            }
        }
    }
}
