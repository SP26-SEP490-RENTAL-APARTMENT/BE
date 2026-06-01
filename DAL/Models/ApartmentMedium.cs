using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class ApartmentMedium
{
    public Guid MediaId { get; set; }

    public Guid ApartmentId { get; set; }

    public string Url { get; set; } = null!;

    public string MediaType { get; set; } = null!;

    public bool? IsPrimary { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;
}
