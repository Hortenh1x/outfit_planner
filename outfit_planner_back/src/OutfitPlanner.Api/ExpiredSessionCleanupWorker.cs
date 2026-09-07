using OutfitPlanner.Application.Services;

namespace OutfitPlanner.Api;

// Periodically removes expired/revoked auth sessions so the sessions table does not grow
// forever. AuthService.CleanupExpiredSessions existed without a caller; this is that caller.
public sealed class ExpiredSessionCleanupWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly AuthService _auth;
    private readonly ILogger<ExpiredSessionCleanupWorker> _logger;

    public ExpiredSessionCleanupWorker(AuthService auth, ILogger<ExpiredSessionCleanupWorker> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var removed = _auth.CleanupExpiredSessions();
                    if (removed > 0)
                    {
                        _logger.LogInformation("Removed {Count} expired auth sessions.", removed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Expired session cleanup failed; retrying next interval.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
