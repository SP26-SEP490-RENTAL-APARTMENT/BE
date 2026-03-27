using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class ReportDefinition
{
    public Guid ReportId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// standard, custom, scheduled, real_time
    /// </summary>
    public string Type { get; set; } = "standard";

    public string Category { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ReportQueryConfig? QueryConfig { get; set; }

    public virtual ICollection<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();

    public virtual ICollection<GeneratedReport> GeneratedReports { get; set; } = new List<GeneratedReport>();
}
