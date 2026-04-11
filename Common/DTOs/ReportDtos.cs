using System;
using System.Collections.Generic;

namespace Common.DTOs;

public class ReportDefinitionDto
{
    public Guid ReportId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ReportRunRequestDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SearchTerm { get; set; }
    public IReadOnlyList<ReportDimensionRequestDto>? Dimensions { get; set; }
    public IReadOnlyList<ReportMetricRequestDto>? Metrics { get; set; }
    public IReadOnlyList<ReportFilterRequestDto>? Filters { get; set; }
}

public class ReportComparisonRequestDto
{
    public string Mode { get; set; } = "custom";
    public ReportRunRequestDto CurrentPeriod { get; set; } = new();
    public ReportRunRequestDto? PreviousPeriod { get; set; }
}

public class ReportExportRequestDto
{
    public string Format { get; set; } = "csv";
    public bool IncludeComparison { get; set; }
    public string? FileName { get; set; }
    public ReportRunRequestDto? RunRequest { get; set; }
    public ReportComparisonRequestDto? ComparisonRequest { get; set; }
}

public class ReportExportContentDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "report.bin";
}

public class ReportDimensionRequestDto
{
    public string Field { get; set; } = string.Empty;
    public string? Alias { get; set; }
}

public class ReportMetricRequestDto
{
    public string Field { get; set; } = string.Empty;
    public string Aggregation { get; set; } = "count";
    public string? Alias { get; set; }
}

public class ReportFilterRequestDto
{
    public string Target { get; set; } = "dimension";
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public string? Value { get; set; }
    public IReadOnlyList<string>? Values { get; set; }
}

public class ReportSchemaDto
{
    public IReadOnlyList<string> Dimensions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> MetricFields { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Aggregations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Operators { get; set; } = Array.Empty<string>();
}

public class ReportResultRowDto
{
    public Dictionary<string, object?> Dimensions { get; set; } = new();
    public Dictionary<string, decimal> Metrics { get; set; } = new();
}

public class ReportResultDto
{
    public Guid ReportId { get; set; }
    public string Name { get; set; } = null!;
    public IReadOnlyList<ReportResultRowDto> Rows { get; set; } = Array.Empty<ReportResultRowDto>();
}

public class ReportComparisonRowDto
{
    public Dictionary<string, object?> Dimensions { get; set; } = new();
    public Dictionary<string, decimal> CurrentMetrics { get; set; } = new();
    public Dictionary<string, decimal> PreviousMetrics { get; set; } = new();
    public Dictionary<string, decimal> DeltaMetrics { get; set; } = new();
    public Dictionary<string, decimal> DeltaPercentMetrics { get; set; } = new();
}

public class ReportComparisonResultDto
{
    public Guid ReportId { get; set; }
    public string Name { get; set; } = null!;
    public string Mode { get; set; } = "custom";
    public DateTime? CurrentFrom { get; set; }
    public DateTime? CurrentTo { get; set; }
    public DateTime? PreviousFrom { get; set; }
    public DateTime? PreviousTo { get; set; }
    public IReadOnlyList<ReportComparisonRowDto> Rows { get; set; } = Array.Empty<ReportComparisonRowDto>();
}
