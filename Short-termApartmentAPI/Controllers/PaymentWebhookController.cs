using BLL.Services.Interfaces;
using Common.Settings;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using System.Linq;
using System.Text.Json;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/payments/webhook")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly IRepository<DAL.Models.Payment> _paymentRepository;
    private readonly IRepository<DAL.Models.BookingCheckTime> _bookingCheckTimeRepository;
    private readonly IRepository<DAL.Models.Booking> _bookingRepository;
    private readonly IRepository<DAL.Models.BookingCheckTimeStateEvent> _checkTimeEventRepository;
    private readonly StripeSettings _stripeSettings;
    private readonly BLL.Services.Interfaces.IBookingService _bookingService;

    public PaymentWebhookController(
        IStripeService stripeService,
        IRepository<DAL.Models.Payment> paymentRepository,
        IRepository<DAL.Models.BookingCheckTime> bookingCheckTimeRepository,
        IRepository<DAL.Models.Booking> bookingRepository,
        IRepository<DAL.Models.BookingCheckTimeStateEvent> checkTimeEventRepository,
        IOptions<StripeSettings> stripeOptions,
        BLL.Services.Interfaces.IBookingService bookingService)
    {
        _stripeService = stripeService;
        _paymentRepository = paymentRepository;
        _bookingCheckTimeRepository = bookingCheckTimeRepository;
        _bookingRepository = bookingRepository;
        _checkTimeEventRepository = checkTimeEventRepository;
        _stripeSettings = stripeOptions.Value;
        _bookingService = bookingService;
    }

    [HttpPost("stripe")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var sigHeader = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, sigHeader, _stripeSettings.WebhookSecret);

            if (stripeEvent.Type == "checkout.session.completed" || stripeEvent.Type == "checkout.session.async_payment_succeeded")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session != null)
                {
                    // Find payment record
                    var payment = (await _paymentRepository.FindAsync(p => p.TransactionId == session.Id)).FirstOrDefault();
                    if (payment != null)
                    {
                        payment.Status = "success";
                        payment.SettlementStatus = "settled";
                        payment.PaidAt = Common.Utils.VietnamTime.Now;
                        _paymentRepository.Update(payment);
                        await _paymentRepository.SaveChangesAsync();

                        // Update related booking check-time
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

                                // Emit audit event: payment_settled
                                try
                                {
                                    var ev = new DAL.Models.BookingCheckTimeStateEvent
                                    {
                                        EventId = Guid.NewGuid(),
                                        BookingId = bookingId,
                                        CheckTimeId = checkTime.CheckTimeId,
                                        EventType = "payment_settled",
                                        EventData = JsonSerializer.Serialize(new { PaymentId = payment.PaymentId, TransactionId = payment.TransactionId, Amount = payment.Amount, Method = payment.Method }),
                                        TriggerSource = "integration",
                                        TriggerReason = "stripe_checkout_session_completed",
                                        CorrelationId = session.Id,
                                        CreatedBy = null,
                                        CreatedAt = Common.Utils.VietnamTime.Now
                                    };
                                    await _checkTimeEventRepository.AddAsync(ev);
                                    await _checkTimeEventRepository.SaveChangesAsync();
                                }
                                catch { }

                                // Optionally update booking status
                                var booking = await _bookingRepository.GetByIdAsync(bookingId);
                                if (booking != null && string.Equals(booking.Status, "disputed", StringComparison.OrdinalIgnoreCase))
                                {
                                    booking.Status = "completed";
                                    _bookingRepository.Update(booking);
                                    await _bookingRepository.SaveChangesAsync();
                                }

                                try
                                {
                                    await _bookingService.TryFinalizeLandlordFundsReleaseAsync(bookingId, "stripe_webhook");
                                }
                                catch { }

                                // Apply booking-side payment side-effects (recalculate paid amounts and status)
                                try
                                {
                                    if (string.Equals(payment.RelatedEntityType, Common.Enums.PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase) && payment.RelatedEntityId.HasValue)
                                    {
                                        if (string.Equals(payment.PaymentType, Common.Enums.PaymentTypes.deposit.ToString(), StringComparison.OrdinalIgnoreCase)
                                            || string.Equals(payment.PaymentType, Common.Enums.PaymentTypes.upfront.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            await _bookingService.MarkDepositPaidAsync(payment.RelatedEntityId.Value, skipConflictCheck: true);
                                        }
                                        else if (string.Equals(payment.PaymentType, Common.Enums.PaymentTypes.balance.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            await _bookingService.MarkBalancePaidAsync(payment.RelatedEntityId.Value);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }

            if (stripeEvent.Type == "checkout.session.expired" || stripeEvent.Type == "payment_intent.payment_failed")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session != null)
                {
                    var payment = (await _paymentRepository.FindAsync(p => p.TransactionId == session.Id)).FirstOrDefault();
                    if (payment != null)
                    {
                        payment.Status = "failed";
                        payment.SettlementStatus = "failed";
                        _paymentRepository.Update(payment);
                        await _paymentRepository.SaveChangesAsync();

                        // mark checktime back to due so tenant can retry
                        if (payment.RelatedEntityId.HasValue)
                        {
                            var bookingId = payment.RelatedEntityId.Value;
                            var checkTime = (await _bookingCheckTimeRepository.FindAsync(ct => ct.BookingId == bookingId)).FirstOrDefault();
                            if (checkTime != null)
                            {
                                checkTime.FeeSettlementStatus = "due";
                                checkTime.FeeSettlementNotes = "Payment failed or expired. Tenant may retry.";
                                _bookingCheckTimeRepository.Update(checkTime);
                                await _bookingCheckTimeRepository.SaveChangesAsync();

                                    // Emit audit event: payment_failed
                                    try
                                    {
                                        var ev = new DAL.Models.BookingCheckTimeStateEvent
                                        {
                                            EventId = Guid.NewGuid(),
                                            BookingId = bookingId,
                                            CheckTimeId = checkTime.CheckTimeId,
                                            EventType = "payment_failed",
                                            EventData = JsonSerializer.Serialize(new { PaymentId = payment.PaymentId, TransactionId = payment.TransactionId, Method = payment.Method }),
                                            TriggerSource = "integration",
                                            TriggerReason = stripeEvent.Type,
                                            CorrelationId = session.Id,
                                            CreatedBy = null,
                                            CreatedAt = Common.Utils.VietnamTime.Now
                                        };
                                        await _checkTimeEventRepository.AddAsync(ev);
                                        await _checkTimeEventRepository.SaveChangesAsync();
                                    }
                                    catch { }
                            }
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
}
