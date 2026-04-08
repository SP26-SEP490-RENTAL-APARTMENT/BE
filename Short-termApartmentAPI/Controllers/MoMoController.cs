using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoMoApi;
using MoMoApi.Services;
using System.Text.Json;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MomoController : ControllerBase
    {
        private readonly IMomoService _momoService;
        private readonly IPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly IMomoTransactionService _momoTransactionService;
        private readonly ILandlordSubscriptionService _landlordSubscriptionService;
        private readonly ILandlordService _landlordService;
        private readonly ILandlordPayoutService _landlordPayoutService;
        private readonly MomoOptions _options;

        public MomoController(
            IMomoService momoService,
            IPaymentService paymentService,
            IBookingService bookingService,
            IMomoTransactionService momoTransactionService,
            ILandlordSubscriptionService landlordSubscriptionService,
            ILandlordService landlordService,
            ILandlordPayoutService landlordPayoutService,
            IOptions<MomoOptions> options)
        {
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
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Persist raw IPN for audit regardless of signature validity
            var ipnLog = new MomoTransaction
            {
                RequestId = Guid.NewGuid().ToString(),
                PartnerCode = _options.PartnerCode,
                Amount = 0,
                Type = "ipn",
                RequestBody = body,
                Status = "received",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _momoTransactionService.CreateAsync(ipnLog);

            // Validate signature before trusting payload
            var valid = MomoIpnValidator.Validate(body, _options.AccessKey, _options.SecretKey);
            if (!valid)
            {
                ipnLog.Status = "invalid_signature";
                ipnLog.UpdatedAt = DateTime.UtcNow;
                await _momoTransactionService.UpdateAsync(ipnLog);
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

            // Update ipn log with parsed info
            ipnLog.Status = "verified";
            ipnLog.UpdatedAt = DateTime.UtcNow;
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

            if (original != null)
            {
                original.ResponseBody = body;
                original.ResultCode = resultCode;
                original.Message = message ?? string.Empty;
                original.UpdatedAt = DateTime.UtcNow;
                original.Status = resultCode == 0 ? "success" : "failed";
                await _momoTransactionService.UpdateAsync(original);

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
                                payment.PaidAt = DateTime.UtcNow;

                            await _paymentService.UpdateAsync(payment);
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
                                original.UpdatedAt = DateTime.UtcNow;
                                await _momoTransactionService.UpdateAsync(original);
                                Console.WriteLine($"[MoMo] Payment success side-effect failed. paymentId={payment.PaymentId}, error={sideEffectError}");
                            }
                        }
                    }
                }
            }

            // Respond per MoMo expectation — keep response small and quick
            return Ok(new { resultCode = 0, message = "OK" });
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

                    var nowDate = DateOnly.FromDateTime(DateTime.UtcNow);
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
                    subscription.UpdatedAt = DateTime.UtcNow;

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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _momoTransactionService.CreateAsync(ipnLog);

            if (!_momoService.ValidateDisbursementIpnSignature(body))
            {
                ipnLog.Status = "invalid_signature";
                ipnLog.UpdatedAt = DateTime.UtcNow;
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
            ipnLog.UpdatedAt = DateTime.UtcNow;
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
