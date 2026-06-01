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
        public string? CancelUrl { get; set; }
        // Optional buyer information
        public string? BuyerName { get; set; }
        public string? BuyerCompanyName { get; set; }
        public string? BuyerEmail { get; set; }
        public string? BuyerPhone { get; set; }
        public string? BuyerAddress { get; set; }
        public bool? BuyerNotGetInvoice { get; set; }
        public PayOS.Models.V2.PaymentRequests.TaxPercentage? TaxPercentage { get; set; }
        public DateTimeOffset? ExpiredAt { get; set; }
        // Optional items
        public List<PayOsItemDto>? Items { get; set; }
    }

    public class PayOsItemDto
    {
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public long Price { get; set; }
        public string? Unit { get; set; }
        public PayOS.Models.V2.PaymentRequests.TaxPercentage? TaxPercentage { get; set; }
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
        public Guid? PaymentId { get; set; }
    }
}
