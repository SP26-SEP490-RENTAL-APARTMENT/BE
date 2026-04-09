using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Tenant
{
    public Guid TenantId { get; set; }

    public string? PassportId { get; set; }

    public string? Sex { get; set; }

    public DateOnly? Birthday { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting
    /// </summary>
    public string? Nationality { get; set; }

    public string? NationalIdCardNumber { get; set; }

    public string? IdentityVerificationStatus { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<BookingOffer> BookingOffers { get; set; } = new List<BookingOffer>();
    public virtual ICollection<WishlistCollection> WishlistCollections { get; set; } = new List<WishlistCollection>();

    public virtual ICollection<TenantWishlist> Wishlists { get; set; } = new List<TenantWishlist>();

    public virtual User TenantNavigation { get; set; } = null!;
}
