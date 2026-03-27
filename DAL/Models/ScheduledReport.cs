using System;

namespace DAL.Models;

public partial class ScheduledReport
{
    public Guid ScheduledReportId { get; set; }

    public Guid ReportId { get; set; }

    public string? Frequency { get; set; }

    public string? CronExpression { get; set; }

    public DateTime? NextRunAt { get; set; }

    public DateTime? LastRunAt { get; set; }

    public string? DeliveryChannel { get; set; }

    public string? Recipients { get; set; }

    public virtual ReportDefinition Report { get; set; } = null!;
}
