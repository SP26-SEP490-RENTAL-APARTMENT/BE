using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Tenant
{
    public Guid TenantId { get; set; }

    public string? PassportId { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting
    /// </summary>
    public string? Nationality { get; set; }

    public string? IdentityVerificationStatus { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual User TenantNavigation { get; set; } = null!;
}
