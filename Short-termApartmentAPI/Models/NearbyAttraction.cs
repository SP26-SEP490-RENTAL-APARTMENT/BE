using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class NearbyAttraction
{
    public Guid AttractionId { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameVi { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Address { get; set; }

    public string? City { get; set; }
}
