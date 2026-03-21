using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Package
{
    public Guid PackageId { get; set; }

    public Guid ApartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? Currency { get; set; }

    public bool? IsActive { get; set; }

    public int? MaxBookings { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<PackagePackage> PackagePackages { get; set; } = new List<PackagePackage>();
}
