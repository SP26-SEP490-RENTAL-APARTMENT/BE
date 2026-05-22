using System;

namespace DAL.Models;

public partial class ApartmentPricingPolicyApplication
{
    public Guid ApplicationId { get; set; }

    public Guid ApartmentId { get; set; }

    public Guid TemplateId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool? IsEnabled { get; set; }

    public string? OverridesJson { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;

    public virtual PricingRuleTemplate Template { get; set; } = null!;
}