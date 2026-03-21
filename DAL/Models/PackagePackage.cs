using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PackagePackage
{
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }

    public Guid PackageItemId { get; set; }

    public virtual Package Package { get; set; } = null!;

    public virtual PackageItem PackageItem { get; set; } = null!;
}
