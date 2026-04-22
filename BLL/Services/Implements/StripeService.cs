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
    private const string DefaultDeepLinkScheme = "myappscheme://payment";

    private enum DeviceContext
    {
        Ios,
        Android,
        Web
    }

    public StripeService(IOptions<StripeSettings> options)
    {
        _settings = options.Value;
        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<StripeCheckoutResponseDto> CreateCheckoutSessionAsync(StripeCheckoutRequestDto request, CancellationToken cancellationToken = default)
    {
        // Phase A: context initialization and validation.
        var deviceContext = DetectDeviceContext(request.DevicePlatform);

        // Phase B: core transaction execution.
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? (_settings.Currency ?? "usd")
            : request.Currency;

        var successUrl = string.IsNullOrWhiteSpace(request.ReturnUrl)
            ? (string.IsNullOrWhiteSpace(request.SuccessUrl)
                ? _settings.SuccessUrl
                : request.SuccessUrl!)
            : request.ReturnUrl!;

        var cancelUrl = string.IsNullOrWhiteSpace(request.CancelUrl)
            ? _settings.CancelUrl
            : request.CancelUrl!;

        if (string.IsNullOrWhiteSpace(successUrl))
        {
            throw new InvalidOperationException("Stripe return URL is required.");
        }

        if (string.IsNullOrWhiteSpace(cancelUrl))
        {
            cancelUrl = successUrl;
        }

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

        options.Metadata["devicePlatform"] = request.DevicePlatform ?? "web";

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        // Phase C: link synthesis and decision logic.
        var redirectPayload = BuildRedirectPayload(session.Id, session.Url ?? string.Empty, deviceContext);

        return new StripeCheckoutResponseDto
        {
            SessionId = session.Id,
            Url = session.Url ?? string.Empty,
            Success = true,
            StatusCode = 200,
            Message = "Payment processing initiated successfully. Use the provided URI.",
            RedirectPayload = redirectPayload
        };
    }

    private DeviceContext DetectDeviceContext(string? devicePlatform)
    {
        if (string.IsNullOrWhiteSpace(devicePlatform))
        {
            throw new InvalidOperationException("device_platform is required. Supported values: ios, android, web.");
        }

        return devicePlatform.Trim().ToLowerInvariant() switch
        {
            "ios" => DeviceContext.Ios,
            "android" => DeviceContext.Android,
            "web" => DeviceContext.Web,
            _ => throw new InvalidOperationException("device_platform is invalid. Supported values: ios, android, web.")
        };
    }

    private StripeRedirectPayloadDto BuildRedirectPayload(string sessionId, string stripeWebUrl, DeviceContext deviceContext)
    {
        var deepLinkBase = string.IsNullOrWhiteSpace(_settings.AppDeepLinkScheme)
            ? DefaultDeepLinkScheme
            : _settings.AppDeepLinkScheme.TrimEnd('/');
        var deepLink = $"{deepLinkBase}/{sessionId}";

        return new StripeRedirectPayloadDto
        {
            StripeWebUrl = stripeWebUrl,
            DeepLinkIos = deviceContext == DeviceContext.Ios ? deepLink : null,
            DeepLinkAndroid = deviceContext == DeviceContext.Android ? deepLink : null,
            IsDeepLinkRequired = deviceContext != DeviceContext.Web
        };
    }
}
