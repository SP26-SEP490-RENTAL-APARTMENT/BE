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
