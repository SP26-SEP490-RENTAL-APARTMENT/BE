using DAL.Repository.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Short_termApartmentAPI.Services;

public class CheckTimeEventRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CheckTimeEventRetentionService> _logger;

    public CheckTimeEventRetentionService(IServiceScopeFactory scopeFactory, ILogger<CheckTimeEventRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CheckTimeEventRetention] Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var repository = scope.ServiceProvider.GetRequiredService<IRepository<DAL.Models.BookingCheckTimeStateEvent>>();

                var retentionDays = config.GetValue<int>("BookingCheckTimeSettings:EventRetentionDays", 365);
                if (retentionDays <= 0) retentionDays = 365;

                var cutoff = Common.Utils.VietnamTime.Now.AddDays(-retentionDays);
                var oldEvents = (await repository.FindAsync(e => e.CreatedAt <= cutoff)).ToList();
                if (oldEvents.Any())
                {
                    foreach (var ev in oldEvents)
                    {
                        repository.Remove(ev);
                    }
                    var removed = await repository.SaveChangesAsync();
                    _logger.LogInformation("[CheckTimeEventRetention] Removed {Count} old events older than {Days} days.", removed, retentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckTimeEventRetention] Error while pruning events.");
            }

            // Run once a day
            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[CheckTimeEventRetention] Stopped.");
    }
}
