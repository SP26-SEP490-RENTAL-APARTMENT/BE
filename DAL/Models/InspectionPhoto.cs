using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class InspectionPhoto
{
    public Guid PhotoId { get; set; }

    public Guid InspectionId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileKey { get; set; }

    public string? Description { get; set; }

    public bool? IsIssue { get; set; }

    public DateTime? UploadedAt { get; set; }

    public virtual PropertyInspection Inspection { get; set; } = null!;
}
