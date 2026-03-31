using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class TenantWishlist
{
    public Guid WishlistId { get; set; }

    public Guid TenantId { get; set; }

    public Guid ApartmentId { get; set; }

    public bool IsFavorite { get; set; } = false;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
