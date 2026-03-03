using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class SmartPricingHistory
{
    public Guid PricingId { get; set; }

    public Guid ApartmentId { get; set; }

    public DateOnly Date { get; set; }

    public decimal SuggestedPrice { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? Multiplier { get; set; }

    public string? Reason { get; set; }

    public decimal? OccupancyRate { get; set; }

    public bool? AcceptedByLandlord { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;
}
