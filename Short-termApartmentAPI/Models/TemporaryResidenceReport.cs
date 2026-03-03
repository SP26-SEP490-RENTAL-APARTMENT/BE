using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class TemporaryResidenceReport
{
    public Guid ReportId { get; set; }

    public Guid BookingId { get; set; }

    public Guid LandlordId { get; set; }

    public string TenantPassportId { get; set; } = null!;

    public string TenantNationality { get; set; } = null!;

    public DateOnly CheckInDate { get; set; }

    public bool? ReportedToPolice { get; set; }

    public DateOnly? ReportDate { get; set; }

    public string? ReportNumber { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Landlord Landlord { get; set; } = null!;
}
