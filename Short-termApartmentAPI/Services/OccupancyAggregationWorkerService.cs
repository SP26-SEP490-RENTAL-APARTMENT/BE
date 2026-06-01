using BLL.Services.Interfaces;

namespace Short_termApartmentAPI.Services;

/// <summary>
/// Background worker that refreshes occupancy cache entries on a fixed schedule.
/// The worker is intentionally thin: it delegates all pricing logic to the BLL service.
/// </summary>
public sealed class OccupancyAggregationWorkerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    private static readonly int ForecastWindowDays = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OccupancyAggregationWorkerService> _logger;

    public OccupancyAggregationWorkerService(
        IServiceScopeFactory scopeFactory,
        ILogger<OccupancyAggregationWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Occupancy Aggregation Worker] Starting background job.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var aggregationService = scope.ServiceProvider.GetRequiredService<IOccupancyAggregationService>();

                var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var endDate = startDate.AddDays(ForecastWindowDays);

                _logger.LogDebug(
                    "[Occupancy Aggregation Worker] Refreshing cache for {StartDate} to {EndDate}.",
                    startDate,
                    endDate);

                await aggregationService.RefreshCacheAsync(startDate, endDate, stoppingToken);

                _logger.LogDebug("[Occupancy Aggregation Worker] Cache refresh completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[Occupancy Aggregation Worker] Shutdown requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Occupancy Aggregation Worker] Unexpected error during cache refresh.");
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

        _logger.LogInformation("[Occupancy Aggregation Worker] Stopped.");
    }
}