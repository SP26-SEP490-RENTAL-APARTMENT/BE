using System;

namespace DAL.Models;

public partial class BookingOffer
{
    public Guid OfferId { get; set; }

    public Guid OriginalBookingId { get; set; }

    public Guid AlternativeApartmentId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? CreatedByStaffId { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal AlternativePrice { get; set; }

    public decimal PriceDifference { get; set; }

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public string? TenantResponseNotes { get; set; }

    public virtual Apartment AlternativeApartment { get; set; } = null!;

    public virtual User? CreatedByStaff { get; set; }

    public virtual Booking OriginalBooking { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
