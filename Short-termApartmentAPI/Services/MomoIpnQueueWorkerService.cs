using BLL.Services.Interfaces;
using DAL.Models;
using Microsoft.Extensions.Options;
using MoMoApi;

namespace Short_termApartmentAPI.Services;

public class MomoIpnQueueWorkerService : BackgroundService
{
    private const int BatchSize = 10;
    private const int MaxAttempts = 10;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MomoIpnQueueWorkerService> _logger;
    private readonly MomoOptions _options;

    public MomoIpnQueueWorkerService(
        IServiceScopeFactory scopeFactory,
        ILogger<MomoIpnQueueWorkerService> logger,
        IOptions<MomoOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var momoTransactionService = scope.ServiceProvider.GetRequiredService<IMomoTransactionService>();
                var webhookService = scope.ServiceProvider.GetRequiredService<IMomoWebhookService>();

                var retryDelay = TimeSpan.FromMinutes(_options.PaymentStatusRetryDelayMinutes);
                var retryReadyBefore = Common.Utils.VietnamTime.Now.Subtract(retryDelay);
                var items = await momoTransactionService.GetPendingIpnQueueItemsAsync(BatchSize, retryReadyBefore);

                foreach (var item in items)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await ProcessQueueItemAsync(item, momoTransactionService, webhookService, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MoMo Queue Worker] Unexpected polling error.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessQueueItemAsync(
        MomoTransaction item,
        IMomoTransactionService momoTransactionService,
        IMomoWebhookService webhookService,
        CancellationToken stoppingToken)
    {
        var attempts = item.ResultCode ?? 0;

        item.Status = "processing";
        item.UpdatedAt = Common.Utils.VietnamTime.Now;
        item.Message = "processing";
        await momoTransactionService.UpdateAsync(item);

        try
        {
            await webhookService.ProcessQueuedIpnEnvelopeAsync(item.RequestBody, stoppingToken);

            item.Status = "processed";
            item.UpdatedAt = Common.Utils.VietnamTime.Now;
            item.Message = "processed";
            await momoTransactionService.UpdateAsync(item);

            _logger.LogInformation("[MoMo Queue Worker] Processed queue message. queueRequestId={QueueRequestId}", item.RequestId);
        }
        catch (Exception ex)
        {
            attempts++;
            item.ResultCode = attempts;
            item.UpdatedAt = Common.Utils.VietnamTime.Now;

            if (attempts >= MaxAttempts)
            {
                item.Status = "dead";
                item.Message = $"dead_after_{attempts}_attempts: {ex.Message}";
                _logger.LogError(ex, "[MoMo Queue Worker] Message moved to dead state after max retries. queueRequestId={QueueRequestId}", item.RequestId);
            }
            else
            {
                item.Status = "retry_wait";
                item.Message = $"retry_{attempts}_scheduled: {ex.Message}";
                _logger.LogWarning(ex, "[MoMo Queue Worker] Processing failed, will retry. queueRequestId={QueueRequestId}, attempts={Attempts}", item.RequestId, attempts);
            }

            await momoTransactionService.UpdateAsync(item);
        }
    }
}
