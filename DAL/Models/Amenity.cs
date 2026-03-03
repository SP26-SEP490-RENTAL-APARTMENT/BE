using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Amenity
{
    public Guid AmenityId { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameVi { get; set; } = null!;

    public virtual ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();
}
