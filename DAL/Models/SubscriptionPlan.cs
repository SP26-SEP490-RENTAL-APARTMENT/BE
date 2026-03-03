using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class SubscriptionPlan
{
    public Guid PlanId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal PriceMonthly { get; set; }

    public decimal? PriceAnnual { get; set; }

    public int? MaxApartments { get; set; }

    public int? MaxApartmentsPerApartment { get; set; }

    public string? Features { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<LandlordSubscription> LandlordSubscriptions { get; set; } = new List<LandlordSubscription>();

    public virtual ICollection<Landlord> Landlords { get; set; } = new List<Landlord>();
}
