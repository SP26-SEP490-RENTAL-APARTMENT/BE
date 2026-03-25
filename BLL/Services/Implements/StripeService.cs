using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace BLL.Services.Implements;

public class StripeService : IStripeService
{
    private readonly StripeSettings _settings;

    public StripeService(IOptions<StripeSettings> options)
    {
        _settings = options.Value;
        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<StripeCheckoutResponseDto> CreateCheckoutSessionAsync(StripeCheckoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? (_settings.Currency ?? "usd")
            : request.Currency;

        var successUrl = string.IsNullOrWhiteSpace(request.SuccessUrl)
            ? _settings.SuccessUrl
            : request.SuccessUrl!;

        var cancelUrl = string.IsNullOrWhiteSpace(request.CancelUrl)
            ? _settings.CancelUrl
            : request.CancelUrl!;

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = request.Amount,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.PaymentPurpose ?? "Booking payment",
                        },
                    },
                    Quantity = 1,
                },
            },
            Metadata = new Dictionary<string, string>()
        };

        if (request.RelatedEntityId.HasValue)
        {
            options.Metadata["relatedEntityId"] = request.RelatedEntityId.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(request.PaymentType))
        {
            options.Metadata["paymentType"] = request.PaymentType!;
        }

        if (!string.IsNullOrWhiteSpace(request.PaymentPurpose))
        {
            options.Metadata["paymentPurpose"] = request.PaymentPurpose!;
        }

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new StripeCheckoutResponseDto
        {
            SessionId = session.Id,
            Url = session.Url ?? string.Empty
        };
    }
}
