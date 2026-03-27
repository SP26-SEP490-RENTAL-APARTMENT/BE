using System;

namespace DAL.Models;

public partial class GeneratedReport
{
    public Guid GeneratedReportId { get; set; }

    public Guid ReportId { get; set; }

    public Guid RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; }

    public string Status { get; set; } = "completed";

    public string? ResultSummaryJson { get; set; }

    public string? ResultJson { get; set; }

    public DateTime? RetentionUntil { get; set; }

    public virtual ReportDefinition Report { get; set; } = null!;
}
