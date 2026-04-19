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

        var ipnLog = new MomoTransaction
        {
            RequestId = Guid.NewGuid().ToString(),
            PartnerCode = _options.PartnerCode,
            Amount = 0,
            Type = "ipn",
            RequestBody = body,
            ResponseBody = string.Empty,
            Status = "received",
            Message = "received",
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };
        await _momoTransactionService.CreateAsync(ipnLog);
        _logger.LogInformation("[MoMo IPN] Inserted ipn transaction row. transactionId={TransactionId}, status={Status}", ipnLog.Id, ipnLog.Status);

        var valid = MomoIpnValidator.Validate(body, _options.AccessKey, _options.SecretKey);
        if (!valid)
        {
            ipnLog.Status = "invalid_signature";
            ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
            await _momoTransactionService.UpdateAsync(ipnLog);
            _logger.LogWarning("[MoMo IPN] Invalid signature. transactionId={TransactionId}", ipnLog.Id);
            return new BadRequestObjectResult(new { resultCode = -1, message = "Invalid signature" });
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var resultCode = root.TryGetProperty("resultCode", out var rc) && rc.ValueKind == JsonValueKind.Number
            ? rc.GetInt32()
            : -1;

        var requestId = root.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String ? rid.GetString() : null;
        var orderId = root.TryGetProperty("orderId", out var oi) && oi.ValueKind == JsonValueKind.String ? oi.GetString() : null;
        var transId = root.TryGetProperty("transId", out var tid)
            ? tid.ValueKind switch
            {
                JsonValueKind.String => tid.GetString(),
                JsonValueKind.Number => tid.GetRawText(),
                _ => null
            }
            : null;
        var message = root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String ? msg.GetString() : string.Empty;

        _logger.LogInformation(
            "[MoMo IPN] Parsed payload. requestId={RequestId}, orderId={OrderId}, resultCode={ResultCode}",
            requestId,
            orderId,
            resultCode);

        ipnLog.Status = "verified";
        ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
        ipnLog.ResponseBody = body;
        ipnLog.ResultCode = resultCode;
        ipnLog.Message = message ?? string.Empty;
        await _momoTransactionService.UpdateAsync(ipnLog);

        MomoTransaction? original = null;
        if (!string.IsNullOrEmpty(requestId))
        {
            original = await _momoTransactionService.FindByRequestIdAsync(requestId);
        }

        if (original == null && !string.IsNullOrEmpty(orderId))
        {
            original = await _momoTransactionService.FindByRequestBodyContainsAsync(orderId);
        }

        _logger.LogInformation(
            "[MoMo IPN] Original transaction lookup. requestId={RequestId}, orderId={OrderId}, found={Found}, originalTransactionId={OriginalTransactionId}",
            requestId,
            orderId,
            original != null,
            original?.Id);

        if (original != null)
        {
            original.ResponseBody = body;
            original.ResultCode = resultCode;
            original.Message = message ?? string.Empty;
            original.UpdatedAt = Common.Utils.VietnamTime.Now;
            original.Status = resultCode == 0 ? "success" : "failed";
            await _momoTransactionService.UpdateAsync(original);
            _logger.LogInformation(
                "[MoMo IPN] Updated original transaction. transactionId={TransactionId}, resultCode={ResultCode}, status={Status}",
                original.Id,
                resultCode,
                original.Status);

            if (original.PaymentId.HasValue)
            {
                var payment = await _paymentService.GetByIdAsync(original.PaymentId.Value);
                if (payment != null)
                {
                    if (payment.Status != "success")
                    {
                        payment.TransactionId = transId;
                        payment.Status = resultCode == 0 ? "success" : "failed";
                        if (resultCode == 0)
                        {
                            payment.PaidAt = Common.Utils.VietnamTime.Now;
                            payment.PlatformFee = Math.Round(payment.Amount * 0.30m, 2, MidpointRounding.AwayFromZero);
                            payment.LandlordAmount = Math.Round(payment.Amount * 0.70m, 2, MidpointRounding.AwayFromZero);
                            payment.SettlementStatus = "pending";
                        }

                        await _paymentService.UpdateAsync(payment);
                        _logger.LogInformation(
                            "[MoMo IPN] Updated payment. paymentId={PaymentId}, resultCode={ResultCode}, status={Status}",
                            payment.PaymentId,
                            resultCode,
                            payment.Status);
                    }
                    else
                    {
                        if (payment.PlatformFee == 0 && payment.LandlordAmount == 0 && resultCode == 0)
                        {
                            payment.PlatformFee = Math.Round(payment.Amount * 0.30m, 2, MidpointRounding.AwayFromZero);
                            payment.LandlordAmount = Math.Round(payment.Amount * 0.70m, 2, MidpointRounding.AwayFromZero);
                            payment.SettlementStatus = "pending";
                            await _paymentService.UpdateAsync(payment);
                            _logger.LogInformation(
                                "[MoMo IPN] Backfilled payment fee split. paymentId={PaymentId}, resultCode={ResultCode}",
                                payment.PaymentId,
                                resultCode);
                        }
                    }

                    if (resultCode == 0 && payment.RelatedEntityId.HasValue
                        && !string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.host_subscription.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        var sideEffectError = await ApplySuccessSideEffectsAsync(payment);
                        if (!string.IsNullOrWhiteSpace(sideEffectError))
                        {
                            original.Status = "success_side_effect_failed";
                            original.Message = $"{(message ?? string.Empty)} | Side-effect failed: {sideEffectError}";
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
                    else if (resultCode == 0
                        && string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.host_subscription.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "[MoMo IPN] host_subscription payment marked success. Activation deferred to /api/Momo/subscription/reconcile. paymentId={PaymentId}, relatedEntityId={RelatedEntityId}",
                            payment.PaymentId,
                            payment.RelatedEntityId);
                    }
                }
            }
        }
        else
        {
            _logger.LogWarning(
                "[MoMo IPN] No original transaction matched. requestId={RequestId}, orderId={OrderId}, resultCode={ResultCode}",
                requestId,
                orderId,
                resultCode);
        }

        return new NoContentResult();
    }

    public async Task<IActionResult> ReconcileSubscriptionPaymentAsync(ReconcileLandlordSubscriptionPaymentRequestDto dto)
    {
        const string successUrl = "http://localhost:5173/landlord/my-subscriptions";

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
        const string successUrl = "http://localhost:5173/payment/success";

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
