using System;

namespace DAL.Models;

public partial class BookingOccupant
{
    public Guid OccupantId { get; set; }

    public Guid BookingId { get; set; }

    public int OccupantOrder { get; set; }

    public bool IsPrimary { get; set; }

    public string? FullName { get; set; }

    public string? PassportId { get; set; }

    public string? NationalIdCardNumber { get; set; }

    public string? Nationality { get; set; }

    public string? Sex { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? ProofPhotoUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
