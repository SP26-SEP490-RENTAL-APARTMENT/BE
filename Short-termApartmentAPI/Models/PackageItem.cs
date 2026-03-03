using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class PackageItem
{
    public Guid PackageItemId { get; set; }

    public Guid PackageId { get; set; }

    public string ItemName { get; set; } = null!;

    public string? ItemDescription { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? EstimatedValue { get; set; }

    public int? SortOrder { get; set; }

    public virtual Package Package { get; set; } = null!;
}
