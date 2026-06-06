using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoMoApi;
using MoMoApi.Services;
using System.Text.Json;

namespace Short_termApartmentAPI.Services;

public class MomoWebhookService : IMomoWebhookService
{
    private const string IpnQueueType = "ipn_queue";
    private const string IpnQueueRequestPrefix = "QIPN:";

    private readonly ILogger<MomoWebhookService> _logger;
    private readonly IMomoService _momoService;
    private readonly IPaymentService _paymentService;
    private readonly IBookingService _bookingService;
    private readonly IMomoTransactionService _momoTransactionService;
    private readonly ILandlordSubscriptionService _landlordSubscriptionService;
    private readonly ILandlordService _landlordService;
    private readonly ILandlordPayoutService _landlordPayoutService;
    private readonly MomoOptions _options;

    public MomoWebhookService(
        ILogger<MomoWebhookService> logger,
        IMomoService momoService,
        IPaymentService paymentService,
        IBookingService bookingService,
        IMomoTransactionService momoTransactionService,
        ILandlordSubscriptionService landlordSubscriptionService,
        ILandlordService landlordService,
        ILandlordPayoutService landlordPayoutService,
        IOptions<MomoOptions> options)
    {
        _logger = logger;
        _momoService = momoService;
        _paymentService = paymentService;
        _bookingService = bookingService;
        _momoTransactionService = momoTransactionService;
        _landlordSubscriptionService = landlordSubscriptionService;
        _landlordService = landlordService;
        _landlordPayoutService = landlordPayoutService;
        _options = options.Value;
    }

    public async Task<IActionResult> HandleIpnAsync(HttpContext httpContext, string body)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var remotePort = httpContext.Connection.RemotePort;
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var forwardedProto = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
        var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
        var host = httpContext.Request.Host.HasValue ? httpContext.Request.Host.Value : string.Empty;
        var traceId = httpContext.TraceIdentifier;

        _logger.LogInformation(
            "[MoMo IPN] Source meta. traceId={TraceId}, method={Method}, path={Path}, host={Host}, remoteIp={RemoteIp}, remotePort={RemotePort}, xForwardedFor={XForwardedFor}, xForwardedProto={XForwardedProto}, userAgent={UserAgent}",
            traceId,
            httpContext.Request.Method,
            httpContext.Request.Path,
            host,
            remoteIp,
            remotePort,
            forwardedFor,
            forwardedProto,
            userAgent);

        _logger.LogInformation("[MoMo IPN] Received callback. bodyLength={BodyLength}", body.Length);

        await ProcessIpnCoreAsync(body, validateSignature: true, throwOnProcessingError: false, transactionType: "ipn");
        return new OkObjectResult(new { resultCode = 0, message = "IPN acknowledged" });
    }

    public async Task<IActionResult> HandleWebhookListenerAsync(HttpContext httpContext, string body)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var remotePort = httpContext.Connection.RemotePort;
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var forwardedProto = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
        var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
        var host = httpContext.Request.Host.HasValue ? httpContext.Request.Host.Value : string.Empty;
        var traceId = httpContext.TraceIdentifier;

        _logger.LogInformation(
            "[MoMo Listener] Callback received. traceId={TraceId}, method={Method}, path={Path}, host={Host}, remoteIp={RemoteIp}, remotePort={RemotePort}, xForwardedFor={XForwardedFor}, xForwardedProto={XForwardedProto}, userAgent={UserAgent}, bodyLength={BodyLength}",
            traceId,
            httpContext.Request.Method,
            httpContext.Request.Path,
            host,
            remoteIp,
            remotePort,
            forwardedFor,
            forwardedProto,
            userAgent,
            body.Length);

        if (!TryExtractRequestId(body, out var requestId))
        {
            _logger.LogWarning("[MoMo Listener] Missing requestId. payload ignored. traceId={TraceId}", traceId);
            return new OkObjectResult(new { resultCode = 0, message = "IPN acknowledged" });
        }

        var valid = MomoIpnValidator.Validate(body, _options.AccessKey, _options.SecretKey);
        if (!valid)
        {
            _logger.LogWarning("[MoMo Listener] Invalid signature. requestId={RequestId}, traceId={TraceId}", requestId, traceId);
            return new OkObjectResult(new { resultCode = 0, message = "IPN acknowledged" });
        }

        var queueRequestId = IpnQueueRequestPrefix + requestId;
        var existed = await _momoTransactionService.FindByRequestIdAsync(queueRequestId);
        if (existed != null)
        {
            _logger.LogInformation("[MoMo Listener] Duplicate queue message ignored. requestId={RequestId}, queueRequestId={QueueRequestId}", requestId, queueRequestId);
            return new OkObjectResult(new { resultCode = 0, message = "IPN acknowledged" });
        }

        var envelope = new MomoIpnQueueEnvelope
        {
            RequestId = requestId!,
            Body = body,
            SourceIp = remoteIp,
            SourcePort = remotePort,
            ForwardedFor = forwardedFor,
            ForwardedProto = forwardedProto,
            Host = host,
            Method = httpContext.Request.Method,
            Path = httpContext.Request.Path.ToString(),
            TraceId = traceId,
            ReceivedAtUtc = DateTime.UtcNow,
            Headers = httpContext.Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.Select(v => v ?? string.Empty).ToArray(), StringComparer.OrdinalIgnoreCase)
        };

        var queueItem = new MomoTransaction
        {
            RequestId = queueRequestId,
            PartnerCode = _options.PartnerCode,
            Amount = 0,
            Type = IpnQueueType,
            RequestBody = JsonSerializer.Serialize(envelope),
            ResponseBody = string.Empty,
            Status = "queued",
            ResultCode = 0,
            Message = "queued",
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };
        await _momoTransactionService.CreateAsync(queueItem);

        _logger.LogInformation("[MoMo Listener] Enqueued IPN payload. requestId={RequestId}, queueRequestId={QueueRequestId}", requestId, queueRequestId);

        return new OkObjectResult(new { resultCode = 0, message = "IPN acknowledged" });
    }

    public async Task ProcessQueuedIpnEnvelopeAsync(string envelopeRaw, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        MomoIpnQueueEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<MomoIpnQueueEnvelope>(envelopeRaw);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[MoMo Queue] Invalid queue envelope JSON. Message skipped.");
            return;
        }

        if (envelope == null || string.IsNullOrWhiteSpace(envelope.Body))
        {
            _logger.LogWarning("[MoMo Queue] Empty queue envelope body. Message skipped.");
            return;
        }

        _logger.LogInformation("[MoMo Queue] Processing queued IPN. requestId={RequestId}, sourceIp={SourceIp}, receivedAtUtc={ReceivedAtUtc}", envelope.RequestId, envelope.SourceIp, envelope.ReceivedAtUtc);
        await ProcessIpnCoreAsync(envelope.Body, validateSignature: true, throwOnProcessingError: true, transactionType: "ipn");
    }

    public async Task ProcessQueriedPaymentAsync(string responseBody, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        await ProcessIpnCoreAsync(responseBody, validateSignature: false, throwOnProcessingError: true, transactionType: "query");
    }

    private async Task ProcessIpnCoreAsync(string body, bool validateSignature, bool throwOnProcessingError, string transactionType)
    {

        MomoTransaction? ipnLog = null;
        string? requestId = null;
        string? orderId = null;
        string? transId = null;
        string message = string.Empty;
        var resultCode = -1;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            resultCode = root.TryGetProperty("resultCode", out var rc) && rc.ValueKind == JsonValueKind.Number
                ? rc.GetInt32()
                : -1;

            requestId = root.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String ? rid.GetString() : null;
            orderId = root.TryGetProperty("orderId", out var oi) && oi.ValueKind == JsonValueKind.String ? oi.GetString() : null;
            transId = root.TryGetProperty("transId", out var tid)
                ? tid.ValueKind switch
                {
                    JsonValueKind.String => tid.GetString(),
                    JsonValueKind.Number => tid.GetRawText(),
                    _ => null
                }
                : null;
            message = root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String ? msg.GetString() ?? string.Empty : string.Empty;

            _logger.LogInformation(
                "[MoMo IPN] Parsed payload. requestId={RequestId}, orderId={OrderId}, transId={TransId}, resultCode={ResultCode}",
                requestId,
                orderId,
                transId,
                resultCode);

            ipnLog = new MomoTransaction
            {
                RequestId = Guid.NewGuid().ToString(),
                PartnerCode = _options.PartnerCode,
                Amount = 0,
                Type = transactionType,
                RequestBody = body,
                ResponseBody = body,
                Status = "received",
                ResultCode = resultCode,
                Message = message,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            };
            await _momoTransactionService.CreateAsync(ipnLog);

            if (validateSignature)
            {
                var valid = MomoIpnValidator.Validate(body, _options.AccessKey, _options.SecretKey);
                if (!valid)
                {
                    ipnLog.Status = "invalid_signature";
                    ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                    await _momoTransactionService.UpdateAsync(ipnLog);
                    _logger.LogWarning("[MoMo IPN] Invalid signature. requestId={RequestId}, orderId={OrderId}", requestId, orderId);
                    return;
                }
            }

            MomoTransaction? original = null;
            if (!string.IsNullOrWhiteSpace(requestId))
            {
                original = await _momoTransactionService.FindByRequestIdAsync(requestId);
            }

            if (original == null && !string.IsNullOrWhiteSpace(orderId))
            {
                original = await _momoTransactionService.FindByRequestBodyContainsAsync(orderId);
            }

            if (original == null)
            {
                ipnLog.Status = "unmatched";
                ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                await _momoTransactionService.UpdateAsync(ipnLog);

                _logger.LogWarning(
                    "[MoMo IPN] No original transaction matched. requestId={RequestId}, orderId={OrderId}, resultCode={ResultCode}",
                    requestId,
                    orderId,
                    resultCode);
                return;
            }

            var incomingSuccess = resultCode == 0;
            var originalStatus = (original.Status ?? string.Empty).Trim().ToLowerInvariant();
            var isCompleted = originalStatus is "completed" or "success";

            if (incomingSuccess && isCompleted)
            {
                ipnLog.Status = "duplicate_completed";
                ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                await _momoTransactionService.UpdateAsync(ipnLog);

                _logger.LogInformation(
                    "[MoMo IPN] Duplicate success ignored. requestId={RequestId}, orderId={OrderId}, originalTransactionId={OriginalTransactionId}, originalStatus={OriginalStatus}",
                    requestId,
                    orderId,
                    original.Id,
                    original.Status);
                return;
            }

            if (!incomingSuccess && isCompleted)
            {
                ipnLog.Status = "late_failure_ignored";
                ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                await _momoTransactionService.UpdateAsync(ipnLog);

                _logger.LogWarning(
                    "[MoMo IPN] Ignored late failure for completed transaction. requestId={RequestId}, orderId={OrderId}, incomingResultCode={ResultCode}, originalStatus={OriginalStatus}",
                    requestId,
                    orderId,
                    resultCode,
                    original.Status);
                return;
            }

            original.ResponseBody = body;
            original.ResultCode = resultCode;
            original.Message = message;
            original.UpdatedAt = Common.Utils.VietnamTime.Now;
            original.Status = incomingSuccess ? "completed" : "failed";
            await _momoTransactionService.UpdateAsync(original);

            ipnLog.Status = original.Status;
            ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
            await _momoTransactionService.UpdateAsync(ipnLog);

            _logger.LogInformation(
                "[MoMo IPN] Transition applied. requestId={RequestId}, orderId={OrderId}, originalTransactionId={OriginalTransactionId}, oldStatus={OldStatus}, newStatus={NewStatus}, incomingResultCode={ResultCode}",
                requestId,
                orderId,
                original.Id,
                originalStatus,
                original.Status,
                resultCode);

            if (original.PaymentId.HasValue)
            {
                var payment = await _paymentService.GetByIdAsync(original.PaymentId.Value);
                if (payment != null)
                {
                    if (incomingSuccess)
                    {
                        payment.TransactionId = string.IsNullOrWhiteSpace(payment.TransactionId) ? transId : payment.TransactionId;
                        payment.Status = PaymentStatus.success.ToString();
                        payment.PaidAt = payment.PaidAt ?? Common.Utils.VietnamTime.Now;
                        payment.PlatformFee = Math.Round(payment.Amount * 0.30m, 2, MidpointRounding.AwayFromZero);
                        payment.LandlordAmount = Math.Round(payment.Amount * 0.70m, 2, MidpointRounding.AwayFromZero);
                        payment.SettlementStatus = "pending";
                        await _paymentService.UpdateAsync(payment);

                        if (payment.RelatedEntityId.HasValue
                            && !string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            var sideEffectError = await ApplySuccessSideEffectsAsync(payment);
                            if (!string.IsNullOrWhiteSpace(sideEffectError))
                            {
                                original.Status = "success_side_effect_failed";
                                original.Message = $"{message} | Side-effect failed: {sideEffectError}";
                                original.UpdatedAt = Common.Utils.VietnamTime.Now;
                                await _momoTransactionService.UpdateAsync(original);
                                _logger.LogError(
                                    "[MoMo IPN] Success side-effect failed. paymentId={PaymentId}, originalTransactionId={TransactionId}, error={Error}",
                                    payment.PaymentId,
                                    original.Id,
                                    sideEffectError);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "[MoMo IPN] Success side-effect applied. paymentId={PaymentId}, relatedEntityType={RelatedEntityType}, relatedEntityId={RelatedEntityId}",
                                    payment.PaymentId,
                                    payment.RelatedEntityType,
                                    payment.RelatedEntityId);
                            }
                        }
                        else if (payment.RelatedEntityId.HasValue
                            && string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var booking = await _bookingService.GetByIdAsync(payment.RelatedEntityId.Value);
                                if (booking != null)
                                {
                                    await ActivateBookingFromPaymentAsync(booking, payment);
                                    _logger.LogInformation(
                                        "[MoMo IPN] booking payment activated. paymentId={PaymentId}, relatedEntityId={RelatedEntityId}",
                                        payment.PaymentId,
                                        payment.RelatedEntityId);
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "[MoMo IPN] booking payment success but booking not found. paymentId={PaymentId}, relatedEntityId={RelatedEntityId}",
                                        payment.PaymentId,
                                        payment.RelatedEntityId);
                                }
                            }
                            catch (Exception ex)
                            {
                                original.Status = "success_booking_activation_failed";
                                original.Message = $"{message} | Booking activation failed: {ex.Message}";
                                original.UpdatedAt = Common.Utils.VietnamTime.Now;
                                await _momoTransactionService.UpdateAsync(original);
                                _logger.LogError(
                                    ex,
                                    "[MoMo IPN] booking payment activation failed. paymentId={PaymentId}, originalTransactionId={TransactionId}",
                                    payment.PaymentId,
                                    original.Id);
                            }
                        }
                    }
                    else if (!string.Equals(payment.Status, PaymentStatus.success.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        payment.Status = PaymentStatus.failed.ToString();
                        payment.TransactionId = string.IsNullOrWhiteSpace(payment.TransactionId) ? transId : payment.TransactionId;
                        await _paymentService.UpdateAsync(payment);
                    }
                }
            }
            return;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[MoMo IPN] Invalid JSON payload. body={Body}", body);

            if (ipnLog != null)
            {
                try
                {
                    ipnLog.Status = "invalid_payload";
                    ipnLog.Message = ex.Message;
                    ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                    await _momoTransactionService.UpdateAsync(ipnLog);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "[MoMo IPN] Failed to update ipnLog after JSON error.");
                }
            }
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[MoMo IPN] Processing error. requestId={RequestId}, orderId={OrderId}, transId={TransId}, resultCode={ResultCode}",
                requestId,
                orderId,
                transId,
                resultCode);

            if (ipnLog != null)
            {
                try
                {
                    ipnLog.Status = "processing_error";
                    ipnLog.Message = ex.Message;
                    ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                    await _momoTransactionService.UpdateAsync(ipnLog);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "[MoMo IPN] Failed to update ipnLog after processing error.");
                }
            }

            if (throwOnProcessingError)
            {
                throw;
            }

            return;
        }
    }

    private static bool TryExtractRequestId(string body, out string? requestId)
    {
        requestId = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String)
            {
                var value = rid.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    requestId = value;
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class MomoIpnQueueEnvelope
    {
        public string RequestId { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string[]> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? SourceIp { get; set; }
        public int SourcePort { get; set; }
        public string? ForwardedFor { get; set; }
        public string? ForwardedProto { get; set; }
        public string Host { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public DateTime ReceivedAtUtc { get; set; }
    }

    public async Task<IActionResult> ReconcileSubscriptionPaymentAsync(ReconcileLandlordSubscriptionPaymentRequestDto dto)
    {
        const string successUrl = "https://rental-apartment-web.vercel.app/landlord/my-subscriptions";

        if (dto == null)
        {
            return new BadRequestObjectResult(new { resultCode = -1, message = "Request body is required." });
        }

        if (dto.ResultCode != 0)
        {
            return new BadRequestObjectResult(new { resultCode = dto.ResultCode, message = dto.Message ?? "Payment was not successful." });
        }

        _logger.LogInformation(
            "[MoMo Reconcile] Received fallback request. requestId={RequestId}, orderId={OrderId}, extraData={ExtraData}, resultCode={ResultCode}",
            dto.RequestId,
            dto.OrderId,
            dto.ExtraData,
            dto.ResultCode);

        string normalizedOrderId = dto.OrderId ?? string.Empty;
        string normalizedRequestId = dto.RequestId ?? string.Empty;
        string normalizedExtraData = dto.ExtraData ?? string.Empty;
        string normalizedMessage = dto.Message ?? "Reconciled from frontend success page.";
        string resultCodeText = dto.ResultCode.ToString();

        MomoTransaction? original = null;
        if (!string.IsNullOrWhiteSpace(dto.RequestId))
        {
            original = await _momoTransactionService.FindByRequestIdAsync(dto.RequestId);
        }

        if (original == null && !string.IsNullOrWhiteSpace(dto.OrderId))
        {
            original = await _momoTransactionService.FindByRequestBodyContainsAsync(dto.OrderId);
        }

        LandlordSubscription? subscription = null;
        Payment? payment = null;

        if (original != null && original.PaymentId.HasValue)
        {
            payment = await _paymentService.GetByIdAsync(original.PaymentId.Value);
            if (payment != null && payment.RelatedEntityType == PaymentRelatedEntityType.host_subscription.ToString() && payment.RelatedEntityId.HasValue)
            {
                subscription = await _landlordSubscriptionService.GetByIdAsync(payment.RelatedEntityId.Value);
            }
        }

        if (subscription == null && Guid.TryParse(dto.ExtraData, out var subscriptionIdFromExtraData))
        {
            subscription = await _landlordSubscriptionService.GetByIdAsync(subscriptionIdFromExtraData);
        }

        if (subscription == null)
        {
            return new NotFoundObjectResult(new { resultCode = -1, message = "Subscription could not be resolved from the provided payment data." });
        }

        payment ??= subscription.LastPaymentId.HasValue
            ? await _paymentService.GetByIdAsync(subscription.LastPaymentId.Value)
            : null;

        if (payment == null)
        {
            return new NotFoundObjectResult(new { resultCode = -1, message = "Payment record could not be found for this subscription." });
        }

        if (string.Equals(subscription.Status, Status.active.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(payment.Status, PaymentStatus.success.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return new OkObjectResult(new
            {
                resultCode = 0,
                message = "Subscription is already active.",
                subscriptionId = subscription.SubscriptionId,
                paymentId = payment.PaymentId,
                subscriptionStatus = subscription.Status,
                successUrl
            });
        }

        payment.Status = PaymentStatus.success.ToString();
        payment.PaidAt = payment.PaidAt ?? Common.Utils.VietnamTime.Now;
        payment.PlatformFee = Math.Round(payment.Amount * 0.30m, 2, MidpointRounding.AwayFromZero);
        payment.LandlordAmount = Math.Round(payment.Amount * 0.70m, 2, MidpointRounding.AwayFromZero);
        payment.SettlementStatus = "pending";

        if (!string.IsNullOrWhiteSpace(dto.TransId))
        {
            payment.TransactionId = dto.TransId;
        }

        await _paymentService.UpdateAsync(payment);

        await ActivateSubscriptionFromPaymentAsync(subscription, payment);

        if (original != null)
        {
            original.ResponseBody = BuildReconcilePayload(normalizedOrderId, normalizedRequestId, normalizedExtraData, resultCodeText, normalizedMessage);
            original.ResultCode = dto.ResultCode;
            original.Message = normalizedMessage;
            original.Status = "reconciled";
            original.UpdatedAt = Common.Utils.VietnamTime.Now;
            original.PaymentId = payment.PaymentId;
            await _momoTransactionService.UpdateAsync(original);
        }
        else
        {
            var reconcileRequestId = dto.RequestId ?? Guid.NewGuid().ToString();
            var reconcileLog = new MomoTransaction
            {
                RequestId = reconcileRequestId,
                PartnerCode = _options.PartnerCode,
                Amount = (long)payment.Amount,
                Type = "subscription_reconcile",
                RequestBody = BuildReconcilePayload(normalizedOrderId, normalizedRequestId, normalizedExtraData, resultCodeText, normalizedMessage),
                ResponseBody = BuildReconcileResponsePayload(normalizedOrderId, normalizedRequestId, normalizedExtraData, resultCodeText, normalizedMessage, subscription, payment),
                Status = "reconciled",
                ResultCode = dto.ResultCode,
                Message = normalizedMessage,
                PaymentId = payment.PaymentId,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            };

            await _momoTransactionService.CreateAsync(reconcileLog);
        }

        _logger.LogInformation(
            "[MoMo Reconcile] Subscription activated. subscriptionId={SubscriptionId}, paymentId={PaymentId}, requestId={RequestId}, orderId={OrderId}",
            subscription.SubscriptionId,
            payment.PaymentId,
            dto.RequestId,
            dto.OrderId);

        return new OkObjectResult(new
        {
            resultCode = 0,
            message = "Subscription reconciled successfully.",
            subscriptionId = subscription.SubscriptionId,
            paymentId = payment.PaymentId,
            subscriptionStatus = subscription.Status,
            startDate = subscription.StartDate,
            endDate = subscription.EndDate,
            successUrl
        });
    }

    public async Task<IActionResult> ReconcileBookingPaymentAsync(ReconcileBookingPaymentRequestDto dto)
    {
        const string successUrl = "https://rental-apartment-web.vercel.app/payment/success";

        if (dto == null)
        {
            return new BadRequestObjectResult(new { resultCode = -1, message = "Request body is required." });
        }

        if (dto.ResultCode != 0)
        {
            return new BadRequestObjectResult(new { resultCode = dto.ResultCode, message = dto.Message ?? "Payment was not successful." });
        }

        _logger.LogInformation(
            "[MoMo Reconcile] Received booking fallback request. requestId={RequestId}, orderId={OrderId}, extraData={ExtraData}, resultCode={ResultCode}",
            dto.RequestId,
            dto.OrderId,
            dto.ExtraData,
            dto.ResultCode);

        string normalizedOrderId = dto.OrderId ?? string.Empty;
        string normalizedRequestId = dto.RequestId ?? string.Empty;
        string normalizedExtraData = dto.ExtraData ?? string.Empty;
        string normalizedMessage = dto.Message ?? "Reconciled from frontend success page.";
        string resultCodeText = dto.ResultCode.ToString();

        MomoTransaction? original = null;
        if (!string.IsNullOrWhiteSpace(dto.RequestId))
        {
            original = await _momoTransactionService.FindByRequestIdAsync(dto.RequestId);
        }

        if (original == null && !string.IsNullOrWhiteSpace(dto.OrderId))
        {
            original = await _momoTransactionService.FindByRequestBodyContainsAsync(dto.OrderId);
        }

        Booking? booking = null;
        Payment? payment = null;

        if (original != null && original.PaymentId.HasValue)
        {
            payment = await _paymentService.GetByIdAsync(original.PaymentId.Value);
            if (payment != null && payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString() && payment.RelatedEntityId.HasValue)
            {
                booking = await _bookingService.GetByIdAsync(payment.RelatedEntityId.Value);
            }
        }

        if (booking == null && Guid.TryParse(dto.ExtraData, out var bookingIdFromExtraData))
        {
            booking = await _bookingService.GetByIdAsync(bookingIdFromExtraData);
        }

        if (booking == null)
        {
            return new NotFoundObjectResult(new { resultCode = -1, message = "Booking could not be resolved from the provided payment data." });
        }

        payment ??= original != null && original.PaymentId.HasValue
            ? await _paymentService.GetByIdAsync(original.PaymentId.Value)
            : null;

        if (payment == null)
        {
            return new NotFoundObjectResult(new { resultCode = -1, message = "Payment record could not be found for this booking." });
        }

        if (string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase)
            && string.Equals(payment.Status, PaymentStatus.success.ToString(), StringComparison.OrdinalIgnoreCase)
            && booking.DepositPaid == true)
        {
            return new OkObjectResult(new
            {
                resultCode = 0,
                message = "Booking is already active.",
                bookingId = booking.BookingId,
                paymentId = payment.PaymentId,
                bookingStatus = booking.Status,
                successUrl
            });
        }

        payment.Status = PaymentStatus.success.ToString();
        payment.PaidAt = payment.PaidAt ?? Common.Utils.VietnamTime.Now;
        payment.PlatformFee = Math.Round(payment.Amount * 0.30m, 2, MidpointRounding.AwayFromZero);
        payment.LandlordAmount = Math.Round(payment.Amount * 0.70m, 2, MidpointRounding.AwayFromZero);
        payment.SettlementStatus = "pending";

        if (!string.IsNullOrWhiteSpace(dto.TransId))
        {
            payment.TransactionId = dto.TransId;
        }

        await _paymentService.UpdateAsync(payment);

        await ActivateBookingFromPaymentAsync(booking, payment);

        if (original != null)
        {
            original.ResponseBody = BuildBookingReconcilePayload(normalizedOrderId, normalizedRequestId, normalizedExtraData, resultCodeText, normalizedMessage);
            original.ResultCode = dto.ResultCode;
            original.Message = normalizedMessage;
            original.Status = "reconciled";
            original.UpdatedAt = Common.Utils.VietnamTime.Now;
            original.PaymentId = payment.PaymentId;
            await _momoTransactionService.UpdateAsync(original);
        }
        else
        {
            var reconcileRequestId = dto.RequestId ?? Guid.NewGuid().ToString();
            var reconcileLog = new MomoTransaction
            {
                RequestId = reconcileRequestId,
                PartnerCode = _options.PartnerCode,
                Amount = (long)payment.Amount,
                Type = "booking_reconcile",
                RequestBody = BuildBookingReconcilePayload(normalizedOrderId, normalizedRequestId, normalizedExtraData, resultCodeText, normalizedMessage),
                ResponseBody = BuildBookingReconcileResponsePayload(normalizedOrderId, normalizedRequestId, normalizedExtraData, resultCodeText, normalizedMessage, booking, payment),
                Status = "reconciled",
                ResultCode = dto.ResultCode,
                Message = normalizedMessage,
                PaymentId = payment.PaymentId,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            };

            await _momoTransactionService.CreateAsync(reconcileLog);
        }

        _logger.LogInformation(
            "[MoMo Reconcile] Booking activated. bookingId={BookingId}, paymentId={PaymentId}, requestId={RequestId}, orderId={OrderId}",
            booking.BookingId,
            payment.PaymentId,
            dto.RequestId,
            dto.OrderId);

        return new OkObjectResult(new
        {
            resultCode = 0,
            message = "Booking reconciled successfully.",
            bookingId = booking.BookingId,
            paymentId = payment.PaymentId,
            bookingStatus = booking.Status,
            depositPaid = booking.DepositPaid,
            successUrl
        });
    }

    public async Task<IActionResult> HandleDisbursementIpnAsync(string body)
    {
        var ipnLog = new MomoTransaction
        {
            RequestId = Guid.NewGuid().ToString(),
            PartnerCode = _options.PartnerCode,
            Amount = 0,
            Type = "disbursement_ipn",
            RequestBody = body,
            ResponseBody = string.Empty,
            Status = "received",
            Message = "received",
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };
        await _momoTransactionService.CreateAsync(ipnLog);

        if (!_momoService.ValidateDisbursementIpnSignature(body))
        {
            ipnLog.Status = "invalid_signature";
            ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
            await _momoTransactionService.UpdateAsync(ipnLog);
            return new BadRequestObjectResult(new { resultCode = -1, message = "Invalid signature" });
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
        var resultCode = root.TryGetProperty("resultCode", out var rc) && rc.ValueKind == JsonValueKind.Number ? rc.GetInt32() : -1;

        ipnLog.Status = "verified";
        ipnLog.ResultCode = resultCode;
        ipnLog.Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty;
        ipnLog.ResponseBody = body;
        ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
        await _momoTransactionService.UpdateAsync(ipnLog);

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            await _landlordPayoutService.SyncProcessingPayoutsAsync();
        }

        return new OkObjectResult(new { resultCode = 0, message = "OK" });
    }

    private async Task<string?> ApplySuccessSideEffectsAsync(Payment payment)
    {
        if (!payment.RelatedEntityId.HasValue)
        {
            return null;
        }

        if (string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var booking = await _bookingService.GetByIdAsync(payment.RelatedEntityId.Value);
                if (booking == null)
                {
                    return "Related booking not found.";
                }

                if (string.Equals(payment.PaymentType, PaymentTypes.deposit.ToString(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(payment.PaymentType, PaymentTypes.upfront.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (booking.DepositPaid == true)
                    {
                        return null;
                    }

                    await _bookingService.MarkDepositPaidAsync(payment.RelatedEntityId.Value);
                    return null;
                }

                if (string.Equals(payment.PaymentType, PaymentTypes.balance.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    await _bookingService.MarkBalancePaidAsync(payment.RelatedEntityId.Value);
                    return null;
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        if (string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.host_subscription.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var subscription = await _landlordSubscriptionService.GetByIdAsync(payment.RelatedEntityId.Value);
                if (subscription == null)
                {
                    return "Related subscription not found.";
                }

                await ActivateSubscriptionFromPaymentAsync(subscription, payment);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        if (string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return null;
    }

    private async Task ActivateSubscriptionFromPaymentAsync(LandlordSubscription subscription, Payment payment)
    {
        if (string.Equals(subscription.Status, Status.active.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nowDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);
        var months = string.Equals(subscription.RenewalType, RenewalType.annual.ToString(), StringComparison.OrdinalIgnoreCase) ? 12 : 1;

        subscription.Status = Status.active.ToString();
        subscription.StartDate = nowDate;
        subscription.EndDate = nowDate.AddMonths(months);
        subscription.PaymentMethod = payment.Method;
        subscription.LastPaymentId = payment.PaymentId;
        subscription.UpdatedAt = Common.Utils.VietnamTime.Now;

        await _landlordSubscriptionService.UpdateAsync(subscription);

        var landlord = await _landlordService.GetByIdAsync(subscription.LandlordId);
        if (landlord != null)
        {
            landlord.CurrentPlanId = subscription.PlanId;
            landlord.SubscriptionStatus = SubscriptionStatus.active.ToString();
            landlord.SubscriptionExpiresAt = subscription.EndDate;
            await _landlordService.UpdateAsync(landlord);
        }
    }

    private async Task ActivateBookingFromPaymentAsync(Booking booking, Payment payment)
    {
        if (string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase) && booking.DepositPaid == true)
        {
            return;
        }

        var paymentType = payment.PaymentType?.Trim().ToLowerInvariant();
        if (string.Equals(paymentType, PaymentTypes.balance.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            await _bookingService.MarkBalancePaidAsync(booking.BookingId);
            return;
        }

        await _bookingService.MarkDepositPaidAsync(booking.BookingId);
    }

    private static string BuildBookingReconcilePayload(string orderId, string requestId, string extraData, string resultCode, string message)
    {
        return $"{{" +
            $"\"OrderId\":{QuoteJson(orderId)}," +
            $"\"RequestId\":{QuoteJson(requestId)}," +
            $"\"ExtraData\":{QuoteJson(extraData)}," +
            $"\"ResultCode\":{QuoteJson(resultCode)}," +
            $"\"Message\":{QuoteJson(message)}" +
            $"}}";
    }

    private static string BuildBookingReconcileResponsePayload(
        string orderId,
        string requestId,
        string extraData,
        string resultCode,
        string message,
        Booking booking,
        Payment payment)
    {
        return $"{{" +
            $"\"OrderId\":{QuoteJson(orderId)}," +
            $"\"RequestId\":{QuoteJson(requestId)}," +
            $"\"ExtraData\":{QuoteJson(extraData)}," +
            $"\"ResultCode\":{QuoteJson(resultCode)}," +
            $"\"Message\":{QuoteJson(message)}," +
            $"\"bookingId\":{QuoteJson(booking.BookingId.ToString())}," +
            $"\"paymentId\":{QuoteJson(payment.PaymentId.ToString())}" +
            $"}}";
    }

    private static string BuildReconcilePayload(string orderId, string requestId, string extraData, string resultCode, string message)
    {
        return $"{{" +
            $"\"OrderId\":{QuoteJson(orderId)}," +
            $"\"RequestId\":{QuoteJson(requestId)}," +
            $"\"ExtraData\":{QuoteJson(extraData)}," +
            $"\"ResultCode\":{QuoteJson(resultCode)}," +
            $"\"Message\":{QuoteJson(message)}" +
            $"}}";
    }

    private static string BuildReconcileResponsePayload(
        string orderId,
        string requestId,
        string extraData,
        string resultCode,
        string message,
        LandlordSubscription subscription,
        Payment payment)
    {
        return $"{{" +
            $"\"OrderId\":{QuoteJson(orderId)}," +
            $"\"RequestId\":{QuoteJson(requestId)}," +
            $"\"ExtraData\":{QuoteJson(extraData)}," +
            $"\"ResultCode\":{QuoteJson(resultCode)}," +
            $"\"Message\":{QuoteJson(message)}," +
            $"\"subscriptionId\":{QuoteJson(subscription.SubscriptionId.ToString())}," +
            $"\"paymentId\":{QuoteJson(payment.PaymentId.ToString())}" +
            $"}}";
    }

    private static string QuoteJson(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
