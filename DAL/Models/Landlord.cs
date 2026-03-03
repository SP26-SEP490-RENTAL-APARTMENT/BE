using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Landlord
{
    public Guid LandlordId { get; set; }

    public bool? VerifiedBusiness { get; set; }

    public Guid? CurrentPlanId { get; set; }

    public DateOnly? SubscriptionExpiresAt { get; set; }

    public string? IdentityVerificationStatus { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    public string? SubscriptionStatus { get; set; }

    public virtual ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();

    public virtual SubscriptionPlan? CurrentPlan { get; set; }

    public virtual User LandlordNavigation { get; set; } = null!;

    public virtual ICollection<LandlordSubscription> LandlordSubscriptions { get; set; } = new List<LandlordSubscription>();

    public virtual ICollection<TemporaryResidenceReport> TemporaryResidenceReports { get; set; } = new List<TemporaryResidenceReport>();
}
