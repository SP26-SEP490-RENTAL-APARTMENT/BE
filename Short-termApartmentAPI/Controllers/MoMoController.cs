using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using BLL.Services.Interfaces;
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
        private readonly IMomoTransactionService _momoTransactionService;
        private readonly MomoOptions _options;

        public MomoController(IMomoService momoService, IPaymentService paymentService, IMomoTransactionService momoTransactionService, IOptions<MomoOptions> options)
        {
            _momoService = momoService;
            _paymentService = paymentService;
            _momoTransactionService = momoTransactionService;
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
                var payment = new Payment
                {
                    Amount = Convert.ToDecimal(request.Amount),
                    PaymentType = "deposit",
                    PaymentPurpose = request.OrderInfo,
                    RelatedEntityId = (Guid.TryParse(request.ExtraData, out var reId) ? reId : (Guid?)null),
                    RelatedEntityType = "other",
                    Method = "momo_wallet",
                    Status = "pending"
                };

                await _paymentService.CreateAsync(payment);

                var requestLog = new MomoTransaction
                {
                    RequestId = result.RequestId,
                    PartnerCode = _options.PartnerCode,
                    Amount = request.Amount,
                    Type = "create_wallet_payment",
                    RequestBody = result.RequestRaw ?? string.Empty,
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
                        }
                    }
                }
            }

            // Respond per MoMo expectation — keep response small and quick
            return Ok(new { resultCode = 0, message = "OK" });
        }
    }
}
