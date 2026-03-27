using System;

namespace DAL.Models;

public partial class ReportQueryConfig
{
    public Guid ReportId { get; set; }

    public string? DimensionsJson { get; set; }

    public string? FiltersJson { get; set; }

    public string? MetricsJson { get; set; }

    public string? TimeRangeJson { get; set; }

    public virtual ReportDefinition Report { get; set; } = null!;
}
