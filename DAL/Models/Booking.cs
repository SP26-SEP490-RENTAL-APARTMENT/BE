using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Booking
{
    public Guid BookingId { get; set; }

    public Guid TenantId { get; set; }

    public Guid ApartmentId { get; set; }

    public DateOnly CheckInDate { get; set; }

    public DateOnly CheckOutDate { get; set; }

    public int Nights { get; set; }

    public int? NoOfAdults { get; set; }

    public int? NoOfInfants { get; set; }

    public int? NoOfPets { get; set; }

    public decimal TotalPrice { get; set; }

    public Guid? PackageId { get; set; }

    public decimal? PackagePrice { get; set; }

    public decimal DepositAmount { get; set; }

    public bool? DepositPaid { get; set; }

    public DateOnly BalanceDueDate { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;

    public virtual Package? Package { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual TemporaryResidenceReport? TemporaryResidenceReport { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
