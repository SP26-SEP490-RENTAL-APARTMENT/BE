using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PropertyInspection
{
    public Guid InspectionId { get; set; }

    public Guid ApartmentId { get; set; }

    public Guid InspectorId { get; set; }

    public DateOnly? ScheduledDate { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public string? Status { get; set; }

    public string? OverallCondition { get; set; }

    public string? IssuesFound { get; set; }

    public string? Recommendations { get; set; }

    public bool? ApprovedForListing { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual ICollection<InspectionPhoto> InspectionPhotos { get; set; } = new List<InspectionPhoto>();

    public virtual User Inspector { get; set; } = null!;
}
