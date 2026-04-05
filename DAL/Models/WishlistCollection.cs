using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class WishlistCollection
{
    public Guid CollectionId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<TenantWishlist> WishlistItems { get; set; } = new List<TenantWishlist>();
}
