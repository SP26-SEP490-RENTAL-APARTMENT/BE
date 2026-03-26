using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoMoApi;
using MoMoApi.Services;
using System.Text.Json;

namespace Short_termApartmentAPI.Controllers
{
    public class MomoController : ControllerBase
    {
        private readonly IMomoService _momoService;
        private readonly IPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly IMomoTransactionService _momoTransactionService;
        private readonly ILandlordSubscriptionService _landlordSubscriptionService;
        private readonly ILandlordService _landlordService;
        private readonly MomoOptions _options;

        public MomoController(
            IMomoService momoService,
            IPaymentService paymentService,
            IBookingService bookingService,
            IMomoTransactionService momoTransactionService,
            ILandlordSubscriptionService landlordSubscriptionService,
            ILandlordService landlordService,
            IOptions<MomoOptions> options)
        {
            _momoService = momoService;
            _paymentService = paymentService;
            _bookingService = bookingService;
            _momoTransactionService = momoTransactionService;
            _landlordSubscriptionService = landlordSubscriptionService;
            _landlordService = landlordService;
            _options = options.Value;
        }

        [HttpPost("create-wallet-payment")]
        public async Task<ActionResult<object>> CreateWalletPayment([FromBody] MomoCreatePaymentRequest request, CancellationToken cancellationToken)
        {
            if (request.Amount <= 0)
            {
                return BadRequest("Amount must be greater than zero.");
            }

            var result = await _momoService.CreateWalletPaymentAsync(request, cancellationToken);

            // Only persist payment and request log if MoMo accepted the create request
            if (result.ResultCode == 0)
            {
                var paymentType = (request.PaymentType ?? "deposit").Trim().ToLowerInvariant();
                if (paymentType != "deposit" && paymentType != "balance" && paymentType != "addon" && paymentType != "refund")
                {
                    paymentType = "deposit";
                }

                var paymentPurpose = request.PaymentPurpose;
                if (string.IsNullOrWhiteSpace(paymentPurpose))
                {
                    paymentPurpose = paymentType switch
                    {
                        "balance" => "booking_balance",
                        "addon" => "booking_addon_or_package",
                        _ => "booking_deposit"
                    };
                }

                var payment = new Payment
                {
                    Amount = Convert.ToDecimal(request.Amount),
                    PaymentType = paymentType,
                    PaymentPurpose = paymentPurpose,
                    RelatedEntityId = (Guid.TryParse(request.ExtraData, out var reId) ? reId : (Guid?)null),
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "momo_wallet",
                    Status = "pending"
                };

                Console.WriteLine($"[MoMo] Saving payment with type='{payment.PaymentType}' (DB default purpose expected)");

                await _paymentService.CreateAsync(payment);

                var requestLog = new MomoTransaction
                {
                    RequestId = result.RequestId,
                    PartnerCode = _options.PartnerCode,
                    Amount = request.Amount,
                    Type = "create_wallet_payment",
                    RequestBody = result.RequestRaw ?? string.Empty,
                    ResponseBody = result.ResponseRaw ?? string.Empty,
                    Status = "pending",
                    ResultCode = result.ResultCode,
                    Message = result.Message,
                    PaymentId = payment.PaymentId
                };
                await _momoTransactionService.CreateAsync(requestLog);
            }

            return Ok(result);
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
            var amount = root.TryGetProperty("amount", out var amt) ? amt.GetRawText().Trim('"') : null;

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
                        // Idempotent update: if already success, do nothing
                        if (payment.Status != "success")
                        {
                            payment.TransactionId = transId;
                            payment.Status = resultCode == 0 ? "success" : "failed";
                            if (resultCode == 0)
                                payment.PaidAt = DateTime.UtcNow;

                            await _paymentService.UpdateAsync(payment);

                            if (resultCode == 0 && payment.RelatedEntityId.HasValue)
                            {
                                if (string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        if (string.Equals(payment.PaymentType, PaymentTypes.deposit.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            await _bookingService.MarkDepositPaidAsync(payment.RelatedEntityId.Value);
                                        }
                                        else if (string.Equals(payment.PaymentType, PaymentTypes.balance.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            await _bookingService.MarkBalancePaidAsync(payment.RelatedEntityId.Value);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[MoMo] Booking payment side-effect failed: {ex.Message}");
                                    }
                                }
                                else if (string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.host_subscription.ToString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        var subscription = await _landlordSubscriptionService.GetByIdAsync(payment.RelatedEntityId.Value);
                                        if (subscription != null && !string.Equals(subscription.Status, Status.active.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
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
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[MoMo] Subscription payment side-effect failed: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Respond per MoMo expectation — keep response small and quick
            return Ok(new { resultCode = 0, message = "OK" });
        }
    }
}
