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

    public string Method { get; set; } = null!;

    public string? Status { get; set; }

    public string? TransactionId { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual ICollection<LandlordSubscription> LandlordSubscriptions { get; set; } = new List<LandlordSubscription>();

    public virtual ICollection<MomoTransaction> MomoTransactions { get; set; } = new List<MomoTransaction>();
}
