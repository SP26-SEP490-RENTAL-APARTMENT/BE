using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentType { get; set; } = null!;

    public string PaymentPurpose { get; set; } = null!;

    public string? RelatedEntityType { get; set; }

    public Guid? LandlordId { get; set; }

    public decimal LandlordAmount { get; set; }

    public decimal PlatformFee { get; set; }

    public string SettlementStatus { get; set; } = "pending";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string Method { get; set; } = null!;

    public string? Status { get; set; }

    public string? TransactionId { get; set; }

    public DateTime? PaidAt { get; set; }

    // Offline payment proof and confirmation
    public string? ProofUrl { get; set; }

    public Guid? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public string? PayerAccountNumber { get; set; }
    public string? PayerBankName { get; set; }
    public string? PayerBankBin { get; set; }
    public string? ReceivingAccountNumber { get; set; }
    public string? ReceivingBankBin { get; set; }

    public virtual ICollection<LandlordSubscription> LandlordSubscriptions { get; set; } = new List<LandlordSubscription>();

    public virtual ICollection<MomoTransaction> MomoTransactions { get; set; } = new List<MomoTransaction>();
}
