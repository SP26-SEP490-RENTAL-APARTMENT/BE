using BLL.Services.Interfaces;
using DAL.Repository.Interfaces;

namespace Short_termApartmentAPI.Services;

/// <summary>
/// Background job that automates check-time lifecycle transitions:
/// claim expiry locks, no-show detection, and missing check-out auto-closure.
/// </summary>
public class BookingCheckTimeAutomationWorkerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingCheckTimeAutomationWorkerService> _logger;

    public BookingCheckTimeAutomationWorkerService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingCheckTimeAutomationWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[BookingCheckTime Automation Worker] Starting background job.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                _logger.LogDebug("[BookingCheckTime Automation Worker] Running automation cycle...");
                await bookingService.ProcessCheckTimeAutomationAsync();
                _logger.LogDebug("[BookingCheckTime Automation Worker] Automation cycle completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[BookingCheckTime Automation Worker] Shutdown requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BookingCheckTime Automation Worker] Unexpected error during automation cycle.");

                try
                {
                    using var scopeErr = _scopeFactory.CreateScope();
                    var repo = scopeErr.ServiceProvider.GetService<IRepository<DAL.Models.BookingCheckTimeStateEvent>>();
                    if (repo != null)
                    {
                        var ev = new DAL.Models.BookingCheckTimeStateEvent
                        {
                            EventId = Guid.NewGuid(),
                            BookingId = Guid.Empty,
                            CheckTimeId = null,
                            EventType = "automation_error",
                            EventData = System.Text.Json.JsonSerializer.Serialize(new { Error = ex.Message, Stack = ex.StackTrace }),
                            CreatedBy = null,
                            CreatedAt = Common.Utils.VietnamTime.Now
                        };
                        await repo.AddAsync(ev);
                        await repo.SaveChangesAsync();
                    }
                }
                catch
                {
                    // ignore
                }
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

        _logger.LogInformation("[BookingCheckTime Automation Worker] Stopped.");
    }
}
