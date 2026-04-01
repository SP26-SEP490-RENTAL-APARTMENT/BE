using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace DAL.Models;

public partial class Apartment
{
    public Guid ApartmentId { get; set; }

    public Guid LandlordId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public sbyte? MaxOccupants { get; set; }

    public bool? IsPetAllowed { get; set; }

    public string? Address { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public Point Location { get; set; } = null!;

    public decimal BasePricePerNight { get; set; }

    public string? Status { get; set; }

    public string? BookingStatus { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<ApartmentMedium> ApartmentMedia { get; set; } = new List<ApartmentMedium>();

    public virtual ICollection<ApartmentAvailability> ApartmentAvailabilities { get; set; } = new List<ApartmentAvailability>();

    public virtual ICollection<ApartmentPriceCalendar> ApartmentPriceCalendars { get; set; } = new List<ApartmentPriceCalendar>();

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<BookingOffer> BookingOffers { get; set; } = new List<BookingOffer>();

    public virtual Landlord Landlord { get; set; } = null!;

    public virtual ICollection<Package> Packages { get; set; } = new List<Package>();

    public virtual ICollection<PropertyInspection> PropertyInspections { get; set; } = new List<PropertyInspection>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<TenantWishlist> TenantWishlists { get; set; } = new List<TenantWishlist>();

    public virtual Room? Room { get; set; }

    public virtual ICollection<SmartPricingHistory> SmartPricingHistories { get; set; } = new List<SmartPricingHistory>();

    public virtual ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
}
