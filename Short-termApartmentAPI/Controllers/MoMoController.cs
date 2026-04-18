using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Common.DTOs;
using MoMoApi;
using MoMoApi.Services;
using System.Text.Json;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MomoController : ControllerBase
    {
        private readonly ILogger<MomoController> _logger;
        private readonly IMomoService _momoService;
        private readonly IPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly IMomoTransactionService _momoTransactionService;
        private readonly ILandlordSubscriptionService _landlordSubscriptionService;
        private readonly ILandlordService _landlordService;
        private readonly ILandlordPayoutService _landlordPayoutService;
        private readonly MomoOptions _options;

        public MomoController(
            ILogger<MomoController> logger,
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

        // IPN endpoint that MoMo will POST to (server-to-server)
        [HttpPost("ipn")]
        public async Task<IActionResult> Ipn()
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var remotePort = HttpContext.Connection.RemotePort;
            var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
            var forwardedProto = Request.Headers["X-Forwarded-Proto"].ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            var traceId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "[MoMo IPN] Source meta. traceId={TraceId}, method={Method}, path={Path}, host={Host}, remoteIp={RemoteIp}, remotePort={RemotePort}, xForwardedFor={XForwardedFor}, xForwardedProto={XForwardedProto}, userAgent={UserAgent}",
                traceId,
                Request.Method,
                Request.Path,
                host,
                remoteIp,
                remotePort,
                forwardedFor,
                forwardedProto,
                userAgent);

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("[MoMo IPN] Received callback. bodyLength={BodyLength}", body.Length);

            // Persist raw IPN for audit regardless of signature validity
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

            // Validate signature before trusting payload
            var valid = MomoIpnValidator.Validate(body, _options.AccessKey, _options.SecretKey);
            if (!valid)
            {
                ipnLog.Status = "invalid_signature";
                ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
                await _momoTransactionService.UpdateAsync(ipnLog);
                _logger.LogWarning("[MoMo IPN] Invalid signature. transactionId={TransactionId}", ipnLog.Id);
                return BadRequest(new { resultCode = -1, message = "Invalid signature" });
            }

            // Parse payload
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var resultCode = root.TryGetProperty("resultCode", out var rc) && rc.ValueKind == JsonValueKind.Number
                ? rc.GetInt32()
                : -1;

            var requestId = root.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String ? rid.GetString() : null;
            var orderId = root.TryGetProperty("orderId", out var oi) && oi.ValueKind == JsonValueKind.String ? oi.GetString() : null;
            var transId = root.TryGetProperty("transId", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() : null;
            var message = root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String ? msg.GetString() : string.Empty;

            _logger.LogInformation(
                "[MoMo IPN] Parsed payload. requestId={RequestId}, orderId={OrderId}, resultCode={ResultCode}",
                requestId,
                orderId,
                resultCode);

            // Update ipn log with parsed info
            ipnLog.Status = "verified";
            ipnLog.UpdatedAt = Common.Utils.VietnamTime.Now;
            ipnLog.ResponseBody = body;
            ipnLog.ResultCode = resultCode;
            ipnLog.Message = message ?? string.Empty;
            await _momoTransactionService.UpdateAsync(ipnLog);

            // Find original request log by requestId or orderId
            MomoTransaction? original = null;
            if (!string.IsNullOrEmpty(requestId))
                original = await _momoTransactionService.FindByRequestIdAsync(requestId);

            if (original == null && !string.IsNullOrEmpty(orderId))
                original = await _momoTransactionService.FindByRequestBodyContainsAsync(orderId);

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

                // Update linked payment if exists
                if (original.PaymentId.HasValue)
                {
                    var payment = await _paymentService.GetByIdAsync(original.PaymentId.Value);
                    if (payment != null)
                    {
                        // Idempotent payment update: avoid re-writing already successful payment state.
                        if (payment.Status != "success")
                        {
                            payment.TransactionId = transId;
                            payment.Status = resultCode == 0 ? "success" : "failed";
                            if (resultCode == 0)
                            {
                                payment.PaidAt = Common.Utils.VietnamTime.Now;

                                // Calculate platform fee split (30% platform, 70% landlord)
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
                            // Ensure fee split is populated even for idempotent retries
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

                        // Important: run success side-effects even when payment is already marked success.
                        // This makes callback retries able to heal partial failures (payment updated, booking not updated).
                        if (resultCode == 0 && payment.RelatedEntityId.HasValue)
                        {
                            var sideEffectError = await ApplySuccessSideEffectsAsync(payment);
                            if (!string.IsNullOrWhiteSpace(sideEffectError))
                            {
                                original.Status = "success_side_effect_failed";
                                original.Message = $"{(message ?? string.Empty)} | Side-effect failed: {sideEffectError}";
                                original.UpdatedAt = Common.Utils.VietnamTime.Now;
                                await _momoTransactionService.UpdateAsync(original);
                                Console.WriteLine($"[MoMo] Payment success side-effect failed. paymentId={payment.PaymentId}, error={sideEffectError}");
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

            // Respond per MoMo expectation � keep response small and quick
            return NoContent();
        }

        [HttpPost("subscription/reconcile")]
        public async Task<IActionResult> ReconcileSubscriptionPayment([FromBody] ReconcileLandlordSubscriptionPaymentRequestDto dto)
        {
            const string successUrl = "http://localhost:5173/landlord/my-subscriptions";

            if (dto == null)
            {
                return BadRequest(new { resultCode = -1, message = "Request body is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.ResultCode != 0)
            {
                return BadRequest(new { resultCode = dto.ResultCode, message = dto.Message ?? "Payment was not successful." });
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
            string normalizedTransId = dto.TransId ?? string.Empty;
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
                return NotFound(new { resultCode = -1, message = "Subscription could not be resolved from the provided payment data." });
            }

            payment ??= subscription.LastPaymentId.HasValue
                ? await _paymentService.GetByIdAsync(subscription.LastPaymentId.Value)
                : null;

            if (payment == null)
            {
                return NotFound(new { resultCode = -1, message = "Payment record could not be found for this subscription." });
            }

            if (string.Equals(subscription.Status, Status.active.ToString(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(payment.Status, PaymentStatus.success.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
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
                var matchedOriginal = original;

                matchedOriginal.ResponseBody = $"{{" +
                    $"\"OrderId\":{QuoteJson(normalizedOrderId)}," +
                    $"\"RequestId\":{QuoteJson(normalizedRequestId)}," +
                    $"\"ExtraData\":{QuoteJson(normalizedExtraData)}," +
                    $"\"ResultCode\":{QuoteJson(resultCodeText)}," +
                    $"\"Message\":{QuoteJson(normalizedMessage)}" +
                    $"}}";
                matchedOriginal.ResultCode = dto.ResultCode;
                matchedOriginal.Message = normalizedMessage;
                matchedOriginal.Status = "reconciled";
                matchedOriginal.UpdatedAt = Common.Utils.VietnamTime.Now;
                matchedOriginal.PaymentId = payment.PaymentId;
                await _momoTransactionService.UpdateAsync(matchedOriginal);
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
                    RequestBody = $"{{" +
                        $"\"OrderId\":{QuoteJson(normalizedOrderId)}," +
                        $"\"RequestId\":{QuoteJson(normalizedRequestId)}," +
                        $"\"ExtraData\":{QuoteJson(normalizedExtraData)}," +
                        $"\"ResultCode\":{QuoteJson(resultCodeText)}," +
                        $"\"Message\":{QuoteJson(normalizedMessage)}" +
                        $"}}",
                    ResponseBody = $"{{" +
                        $"\"OrderId\":{QuoteJson(normalizedOrderId)}," +
                        $"\"RequestId\":{QuoteJson(normalizedRequestId)}," +
                        $"\"ExtraData\":{QuoteJson(normalizedExtraData)}," +
                        $"\"ResultCode\":{QuoteJson(resultCodeText)}," +
                        $"\"Message\":{QuoteJson(normalizedMessage)}," +
                        $"\"subscriptionId\":{QuoteJson(subscription.SubscriptionId.ToString())}," +
                        $"\"paymentId\":{QuoteJson(payment.PaymentId.ToString())}" +
                        $"}}",
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

            return Ok(new
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
                    if (subscription == null || string.Equals(subscription.Status, Status.active.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    var nowDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);
                    subscription.Status = Status.active.ToString();
                    subscription.StartDate = nowDate;

                    var months = 1;
                    if (string.Equals(subscription.RenewalType, RenewalType.annual.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        months = 12;
                    }

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

                    return null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            return null;
        }

        private static string QuoteJson(string value)
        {
            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
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

        [HttpPost("disbursement-ipn")]
        public async Task<IActionResult> DisbursementIpn()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

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
                return BadRequest(new { resultCode = -1, message = "Invalid signature" });
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
                // Fast path: run reconciliation sync, which includes pending/processing payouts.
                await _landlordPayoutService.SyncProcessingPayoutsAsync();
            }

            return Ok(new { resultCode = 0, message = "OK" });
        }
    }
}
