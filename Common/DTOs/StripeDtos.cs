using System;
using System.Text.Json.Serialization;

namespace Common.DTOs;

public class StripeCheckoutRequestDto
{
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? DevicePlatform { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? PaymentType { get; set; }
    public string? PaymentPurpose { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public class StripeCheckoutResponseDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public StripeRedirectPayloadDto RedirectPayload { get; set; } = new();
}

public class StripeRedirectPayloadDto
{
    [JsonPropertyName("stripe_web_url")]
    public string StripeWebUrl { get; set; } = string.Empty;

    [JsonPropertyName("deep_link_ios")]
    public string? DeepLinkIos { get; set; }

    [JsonPropertyName("deep_link_android")]
    public string? DeepLinkAndroid { get; set; }

    [JsonPropertyName("is_deep_link_required")]
    public bool IsDeepLinkRequired { get; set; }
}
