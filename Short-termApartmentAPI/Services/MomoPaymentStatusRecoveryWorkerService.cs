using System.Text.Json;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.Extensions.Options;
using MoMoApi;

namespace Short_termApartmentAPI.Services;

public class MomoPaymentStatusRecoveryWorkerService : BackgroundService
{
    private const int BatchSize = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MomoPaymentStatusRecoveryWorkerService> _logger;
    private readonly MomoOptions _options;

    public MomoPaymentStatusRecoveryWorkerService(
        IServiceScopeFactory scopeFactory,
        ILogger<MomoPaymentStatusRecoveryWorkerService> logger,
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
                var momoService = scope.ServiceProvider.GetRequiredService<IMomoService>();
                var webhookService = scope.ServiceProvider.GetRequiredService<IMomoWebhookService>();

                var queryAfter = TimeSpan.FromSeconds(Math.Max(1, _options.PaymentStatusQueryAfterSeconds));
                var queryReadyBefore = Common.Utils.VietnamTime.Now.Subtract(queryAfter);
                var items = await momoTransactionService.GetPendingWalletPaymentRequestsAsync(BatchSize, queryReadyBefore);

                foreach (var item in items)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await ProcessPendingPaymentAsync(item, momoService, webhookService, momoTransactionService, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MoMo Recovery Worker] Unexpected polling error.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingPaymentAsync(
        MomoTransaction item,
        IMomoService momoService,
        IMomoWebhookService webhookService,
        IMomoTransactionService momoTransactionService,
        CancellationToken stoppingToken)
    {
        var parsed = TryParseCreateRequest(item.RequestBody, out var orderId, out var requestId);
        if (!parsed)
        {
            _logger.LogWarning("[MoMo Recovery Worker] Could not parse original create request. requestId={RequestId}", item.RequestId);
            await ScheduleRetryAsync(item, momoTransactionService, "invalid_create_request_format");
            return;
        }

        try
        {
            _logger.LogInformation("[MoMo Recovery Worker] Querying MoMo payment status. requestId={RequestId}, orderId={OrderId}", requestId, orderId);

            var queryResult = await momoService.QueryPaymentStatusAsync(new MomoQueryPaymentRequest
            {
                OrderId = orderId,
                RequestId = requestId,
                Lang = "vi"
            }, stoppingToken);

            if (string.IsNullOrWhiteSpace(queryResult.ResponseRaw))
            {
                queryResult.ResponseRaw = JsonSerializer.Serialize(queryResult);
            }

            await webhookService.ProcessQueriedPaymentAsync(queryResult.ResponseRaw!, stoppingToken);

            _logger.LogInformation("[MoMo Recovery Worker] Query processed. requestId={RequestId}, resultCode={ResultCode}", requestId, queryResult.ResultCode);
        }
        catch (Exception ex)
        {
            await ScheduleRetryAsync(item, momoTransactionService, ex.Message);
            _logger.LogWarning(ex, "[MoMo Recovery Worker] Query failed, will retry later. requestId={RequestId}", item.RequestId);
        }
    }

    private static bool TryParseCreateRequest(string requestBody, out string orderId, out string requestId)
    {
        orderId = string.Empty;
        requestId = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("orderId", out var orderIdElement) && orderIdElement.ValueKind == JsonValueKind.String)
            {
                orderId = orderIdElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("requestId", out var requestIdElement) && requestIdElement.ValueKind == JsonValueKind.String)
            {
                requestId = requestIdElement.GetString() ?? string.Empty;
            }

            return !string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(requestId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task ScheduleRetryAsync(MomoTransaction item, IMomoTransactionService momoTransactionService, string message)
    {
        item.Status = "retry_wait";
        item.Message = message;
        item.UpdatedAt = Common.Utils.VietnamTime.Now;
        item.ResultCode = (item.ResultCode ?? 0) + 1;

        var maxRetryMinutes = _options.PaymentStatusRetryDelayMinutes;
        item.ResponseBody = $"retry_after_minutes={maxRetryMinutes}";

        await momoTransactionService.UpdateAsync(item);
    }
}
