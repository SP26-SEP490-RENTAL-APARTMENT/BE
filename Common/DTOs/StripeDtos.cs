using System;

namespace Common.DTOs;

public class StripeCheckoutRequestDto
{
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
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
}
