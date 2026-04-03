using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Short_termApartmentAPI.Middlewares;
using Stripe;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StripeController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IBookingService _bookingService;
    private readonly Common.Settings.StripeSettings _settings;

    public StripeController(
        IPaymentService paymentService,
        IBookingService bookingService,
        IOptions<Common.Settings.StripeSettings> stripeOptions)
    {
        _paymentService = paymentService;
        _bookingService = bookingService;
        _settings = stripeOptions.Value;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signatureHeader = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signatureHeader))
        {
            return BadRequest();
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _settings.WebhookSecret);
        }
        catch (StripeException)
        {
            return BadRequest();
        }

        if (stripeEvent.Type == Events.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session != null)
            {
                var transactionId = session.Id;

                // Find payment by stored SessionId
                var paymentsRepoField = typeof(BLL.Services.Implements.BaseService<Payment>)
                    .GetField("_repository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var repo = paymentsRepoField?.GetValue(_paymentService) as DAL.Repository.Interfaces.IRepository<Payment>;
                if (repo != null)
                {
                    var matches = await repo.FindAsync(p =>
                        p.Method == "stripe" &&
                        p.TransactionId == transactionId);
                    var payment = matches.FirstOrDefault();
                    if (payment != null && payment.Status != PaymentStatus.success.ToString())
                    {
                        payment.Status = PaymentStatus.success.ToString();
                        payment.PaidAt = DateTime.UtcNow;
                        payment.TransactionId = transactionId;

                        await _paymentService.UpdateAsync(payment);

                        if (payment.RelatedEntityId.HasValue &&
                            string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
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
                                Console.WriteLine($"[Stripe] Booking payment side-effect failed: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        return Ok();
    }
}
