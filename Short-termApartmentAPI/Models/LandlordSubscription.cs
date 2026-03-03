using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class LandlordSubscription
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

    public virtual Landlord Landlord { get; set; } = null!;

    public virtual Payment? LastPayment { get; set; }

    public virtual SubscriptionPlan Plan { get; set; } = null!;
}
