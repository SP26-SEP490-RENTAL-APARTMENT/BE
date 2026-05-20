using System;

namespace Common.DTOs
{
    public class PayOsCreatePaymentRequest
    {
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string? ExtraData { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentPurpose { get; set; }
        public string? RedirectUrl { get; set; }
    }

    public class PayOsCreatePaymentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? Deeplink { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? ResponseRaw { get; set; }

        // Persistence metadata
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string? RequestRaw { get; set; }
    }
}
