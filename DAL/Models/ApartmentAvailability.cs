using System;

namespace DAL.Models;

public partial class ApartmentAvailability
{
    public Guid AvailabilityId { get; set; }

    public Guid ApartmentId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;
}
