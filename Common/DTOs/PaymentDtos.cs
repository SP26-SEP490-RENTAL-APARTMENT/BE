using System;

namespace Common.DTOs
{
    public class PaymentHistoryDto
    {
        public Guid PaymentId { get; set; }
        public Guid? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public decimal Amount { get; set; }
        public string SignedAmountDisplay { get; set; } = string.Empty; 
        public string PaymentType { get; set; } = string.Empty;
        public string PaymentPurpose { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? TransactionId { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PayerAccountNumber { get; set; }
        public string? PayerBankName { get; set; }
        public string? PayerBankBin { get; set; }
        public string? ReceivingAccountNumber { get; set; }
        public string? ReceivingBankBin { get; set; }
    }
}
