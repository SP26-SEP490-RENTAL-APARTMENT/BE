using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StripeController : ControllerBase
{
    private readonly ILogger<StripeController> _logger;
    private readonly IPaymentService _paymentService;
    private readonly IBookingService _bookingService;
    private readonly IConfiguration _configuration;

    public StripeController(
        ILogger<StripeController> logger,
        IPaymentService paymentService,
        IBookingService bookingService,
        IConfiguration configuration)
    {
        _logger = logger;
        _paymentService = paymentService;
        _bookingService = bookingService;
        _configuration = configuration;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        _logger.LogInformation("Stripe-Signature header: {Signature}", signatureHeader);

        // Read raw body as string (and log its length)
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        _logger.LogInformation("Raw JSON length: {Length}, first 100 chars: {Preview}",
            json.Length, json.Substring(0, Math.Min(100, json.Length)));

        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        try
        {
            // 3. Construct and verify the event using the raw JSON string
            var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);

            // 4. Log success for debugging
            _logger.LogInformation("Webhook verified for Event ID: {EventId}, Type: {EventType}", stripeEvent.Id, stripeEvent.Type);


            if (string.IsNullOrEmpty(signatureHeader))
            {
                return BadRequest();
            }

            if (stripeEvent.Type == "checkout.session.completed")
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
                            payment.PaidAt = Common.Utils.VietnamTime.Now;
                            payment.TransactionId = transactionId;

                            await _paymentService.UpdateAsync(payment);

                            if (payment.RelatedEntityId.HasValue &&
                                string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    if (string.Equals(payment.PaymentType, PaymentTypes.deposit.ToString(), StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(payment.PaymentType, PaymentTypes.upfront.ToString(), StringComparison.OrdinalIgnoreCase))
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
            else
            {
                _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
            }
            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Verification failed. Secret used: {SecretPrefix}",
            webhookSecret?.Substring(0, 8));
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing Stripe webhook: {Message}", ex.Message);
            return StatusCode(500);
        }
    }
}
