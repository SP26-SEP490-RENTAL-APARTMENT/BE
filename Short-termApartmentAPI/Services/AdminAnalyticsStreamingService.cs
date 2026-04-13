using System;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Short_termApartmentAPI.Hubs;

namespace Short_termApartmentAPI.Services;

public class AdminAnalyticsStreamingService : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<AdminAnalyticsHub> _hubContext;
    private readonly ILogger<AdminAnalyticsStreamingService> _logger;

    public AdminAnalyticsStreamingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<AdminAnalyticsHub> hubContext,
        ILogger<AdminAnalyticsStreamingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var analyticsService = scope.ServiceProvider.GetRequiredService<IAdminAnalyticsService>();
                var snapshot = await analyticsService.GetSnapshotAsync(stoppingToken);

                await _hubContext.Clients.Group("admins").SendAsync("analyticsSnapshot", snapshot, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish admin analytics snapshot.");
            }

            await Task.Delay(PublishInterval, stoppingToken);
        }
    }
}
