using BLL.Services.Interfaces;

namespace Short_termApartmentAPI.Services;

/// <summary>
/// Background job to automatically expire stale check-time request counter-offers.
/// Runs every 5 minutes to find and update expired counter-offers (ExpiresAt < now).
/// </summary>
public class CheckTimeRequestExpiryWorkerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CheckTimeRequestExpiryWorkerService> _logger;

    public CheckTimeRequestExpiryWorkerService(
        IServiceScopeFactory scopeFactory,
        ILogger<CheckTimeRequestExpiryWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CheckTimeRequest Expiry Worker] Starting background job.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var checkTimeRequestService = scope.ServiceProvider.GetRequiredService<ICheckTimeRequestService>();

                _logger.LogDebug("[CheckTimeRequest Expiry Worker] Polling for expired counter-offers...");

                await checkTimeRequestService.ExpireStaleRequestsAsync();

                _logger.LogDebug("[CheckTimeRequest Expiry Worker] Completed expiry check.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[CheckTimeRequest Expiry Worker] Shutdown requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckTimeRequest Expiry Worker] Unexpected error during expiry check.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[CheckTimeRequest Expiry Worker] Stopped.");
    }
}
