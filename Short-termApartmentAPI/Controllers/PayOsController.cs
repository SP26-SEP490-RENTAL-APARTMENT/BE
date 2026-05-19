using BLL.Services.Interfaces;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/payments/webhook")]
public class PayOsController : ControllerBase
{
    private readonly IPayOsService _payOsService;
    private readonly IRepository<DAL.Models.Payment> _paymentRepository;
    private readonly IRepository<DAL.Models.BookingCheckTime> _bookingCheckTimeRepository;
    private readonly IRepository<DAL.Models.Booking> _bookingRepository;
    private readonly IRepository<DAL.Models.BookingCheckTimeStateEvent> _checkTimeEventRepository;
    private readonly PayOS.PayOSClient? _sdkClient;

    public PayOsController(
        IPayOsService payOsService,
        IRepository<DAL.Models.Payment> paymentRepository,
        IRepository<DAL.Models.BookingCheckTime> bookingCheckTimeRepository,
        IRepository<DAL.Models.Booking> bookingRepository,
        IRepository<DAL.Models.BookingCheckTimeStateEvent> checkTimeEventRepository,
        PayOS.PayOSClient? sdkClient = null)
    {
        _payOsService = payOsService;
        _paymentRepository = paymentRepository;
        _bookingCheckTimeRepository = bookingCheckTimeRepository;
        _bookingRepository = bookingRepository;
        _checkTimeEventRepository = checkTimeEventRepository;
        _sdkClient = sdkClient;
    }

    [HttpPost("payos")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PayOsWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        // Try common header names used for signatures
        var signatureHeader = Request.Headers["PayOS-Signature"].FirstOrDefault()
                              ?? Request.Headers["X-PayOS-Signature"].FirstOrDefault()
                              ?? Request.Headers["Payos-Signature"].FirstOrDefault()
                              ?? Request.Headers["X-Payos-Signature"].FirstOrDefault()
                              ?? string.Empty;

        try
        {
            JsonElement root;

            if (_sdkClient != null)
            {
                // Prefer SDK-based verification
                var webhookModel = System.Text.Json.JsonSerializer.Deserialize<PayOS.Models.Webhooks.Webhook>(json ?? string.Empty);
                var webhookData = await _sdkClient.Webhooks.VerifyAsync(webhookModel!).ConfigureAwait(false);
                if (webhookData == null) return BadRequest(new { error = "Invalid webhook (SDK verification failed)" });

                var verifiedJson = JsonSerializer.Serialize(webhookData);
                using var doc = JsonDocument.Parse(verifiedJson);
                root = doc.RootElement;
            }
            else
            {
                var verified = _payOsService.VerifyWebhookSignature(json, signatureHeader);
                if (!verified) return BadRequest(new { error = "Invalid signature" });

                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement;
            }

            string? reference = GetJsonString(root, "reference", "Reference");
            string? paymentLinkId = GetJsonString(root, "paymentLinkId", "PaymentLinkId", "payment_link_id");

            var payment = (await _paymentRepository.FindAsync(p => p.TransactionId == reference || p.TransactionId == paymentLinkId)).FirstOrDefault();
            if (payment != null)
            {
                payment.Status = "success";
                payment.SettlementStatus = "settled";
                payment.PaidAt = Common.Utils.VietnamTime.Now;
                _paymentRepository.Update(payment);
                await _paymentRepository.SaveChangesAsync();

                // Update related booking check-time (mirror Stripe handler behaviour)
                if (payment.RelatedEntityId.HasValue)
                {
                    var bookingId = payment.RelatedEntityId.Value;
                    var checkTime = (await _bookingCheckTimeRepository.FindAsync(ct => ct.BookingId == bookingId)).FirstOrDefault();
                    if (checkTime != null)
                    {
                        checkTime.FeeSettlementStatus = "paid";
                        checkTime.FeeSettledAt = Common.Utils.VietnamTime.Now;
                        checkTime.FeeDueAt = null;
                        checkTime.ClaimLockedAt ??= Common.Utils.VietnamTime.Now;
                        checkTime.ClaimStatus = "paid";
                        _bookingCheckTimeRepository.Update(checkTime);
                        await _bookingCheckTimeRepository.SaveChangesAsync();

                        try
                        {
                            var ev = new DAL.Models.BookingCheckTimeStateEvent
                            {
                                EventId = Guid.NewGuid(),
                                BookingId = bookingId,
                                CheckTimeId = checkTime.CheckTimeId,
                                EventType = "payment_settled",
                                EventData = JsonSerializer.Serialize(new { PaymentId = payment.PaymentId, TransactionId = payment.TransactionId, Amount = payment.Amount, Method = payment.Method }),
                                CreatedBy = null,
                                CreatedAt = Common.Utils.VietnamTime.Now
                            };
                            await _checkTimeEventRepository.AddAsync(ev);
                            await _checkTimeEventRepository.SaveChangesAsync();
                        }
                        catch { }

                        var booking = await _bookingRepository.GetByIdAsync(bookingId);
                        if (booking != null && string.Equals(booking.Status, "disputed", StringComparison.OrdinalIgnoreCase))
                        {
                            booking.Status = "completed";
                            _bookingRepository.Update(booking);
                            await _bookingRepository.SaveChangesAsync();
                        }
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static string? GetJsonString(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }
}
