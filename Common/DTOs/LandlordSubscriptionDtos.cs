using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class StartLandlordSubscriptionRequestDto
    {
        [Required]
        public Guid PlanId { get; set; }

        /// <summary>
        /// Billing cycle: "monthly" or "annual". Defaults to "monthly".
        /// </summary>
        [MaxLength(20)]
        public string? RenewalType { get; set; }

        /// <summary>
        /// Whether the subscription should auto renew when it expires.
        /// </summary>
        public bool AutoRenew { get; set; } = true;
    }

    public class LandlordSubscriptionHistoryDto
    {
        public Guid SubscriptionId { get; set; }
        public Guid LandlordId { get; set; }
        public Guid PlanId { get; set; }
        public string? Status { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? RenewalType { get; set; }
        public bool? AutoRenew { get; set; }
        public string? PaymentMethod { get; set; }
        public Guid? LastPaymentId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class WalletSubscriptionPaymentResponseDto
    {
        public Guid SubscriptionId { get; set; }
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string? RenewalType { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? SubscriptionStatus { get; set; }
        public decimal RemainingWalletBalance { get; set; }
    }

    public class ReconcileLandlordSubscriptionPaymentRequestDto
    {
        [Required]
        public string? OrderId { get; set; }

        [Required]
        public string? RequestId { get; set; }

        [Required]
        public string? ExtraData { get; set; }

        public int ResultCode { get; set; }

        [MaxLength(1024)]
        public string? Message { get; set; }

        [MaxLength(100)]
        public string? TransId { get; set; }
    }
}
