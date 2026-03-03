using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class ApartmentPriceCalendar
{
    public Guid PriceId { get; set; }

    public Guid ApartmentId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal? DiscountPercentage { get; set; }

    public bool? IsDiscount { get; set; }

    public string? PriceType { get; set; }

    public int? MinNights { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;
}
