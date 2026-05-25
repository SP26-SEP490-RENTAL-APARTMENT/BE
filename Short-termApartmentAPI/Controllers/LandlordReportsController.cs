using BLL.Services.Implements;
using BLL.Services.Interfaces;
using DAL.Data;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/landlord/reports")]
[Authorize(Roles = "landlord")]
public sealed class LandlordReportsController : ControllerBase
{
    private static readonly HashSet<string> AllowedCatalogCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "booking",
        "revenue",
        "review",
        "subscription",
        "compliance"
    };

    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IReportExecutionService _reportExecutionService;
    private readonly IReportExportService _reportExportService;
    private readonly ILandlordService _landlordService;
    private readonly AppDbContext _dbContext;

    public LandlordReportsController(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IReportExecutionService reportExecutionService,
        IReportExportService reportExportService,
        ILandlordService landlordService,
        AppDbContext dbContext)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _reportExecutionService = reportExecutionService;
        _reportExportService = reportExportService;
        _landlordService = landlordService;
        _dbContext = dbContext;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = await _reportDefinitionRepository.FindAsync(r =>
            r.IsActive &&
            AllowedCatalogCategories.Contains(r.Category));

        var ordered = items
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = ordered.Count;
        var pageItems = ordered
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(pageSize)
            .Select(MapDefinition)
            .ToList();

        return Ok(new { Items = pageItems, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<IActionResult> GetSchema(Guid id)
    {
        var definition = await _reportDefinitionRepository.GetByIdAsync(id);
        if (definition == null || !AllowedCatalogCategories.Contains(definition.Category))
        {
            return NotFound(new ApiResponse<string>("Report definition not found."));
        }

        var schema = ReportExecutionService.GetDefaultSchema();
        return Ok(new ApiResponse<ReportSchemaDto>(schema));
    }

    [HttpGet("{id:guid}/config")]
    public async Task<IActionResult> GetConfig(Guid id)
    {
        var definition = await _reportDefinitionRepository.GetByIdAsync(id);
        if (definition == null || !AllowedCatalogCategories.Contains(definition.Category))
        {
            return NotFound(new ApiResponse<string>("Report definition not found."));
        }

        var config = await _dbContext.Set<ReportQueryConfig>().FindAsync(id);
        if (config == null)
        {
            return NotFound(new ApiResponse<string>("Report config not found."));
        }

        return Ok(new ApiResponse<ReportQueryConfigResponseDto>(new ReportQueryConfigResponseDto
        {
            DimensionsJson = config.DimensionsJson,
            MetricsJson = config.MetricsJson,
            FiltersJson = config.FiltersJson,
            TimeRangeJson = config.TimeRangeJson
        }));
    }

    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, [FromBody] ReportRunRequestDto request)
    {
        var (userId, landlord) = await ResolveLandlordContextAsync();
        if (landlord is null)
        {
            return Unauthorized(new ApiResponse<string>("Landlord profile not found."));
        }

        try
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Max(1, request.PageSize);
            var result = await _reportExecutionService.RunReportPageAsync(id, request, page, pageSize, userId, landlord.LandlordId);
            return Ok(new ApiResponse<ReportResultPageDto>(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPost("{id:guid}/compare")]
    public async Task<IActionResult> Compare(Guid id, [FromBody] ReportComparisonRequestDto request)
    {
        var (userId, landlord) = await ResolveLandlordContextAsync();
        if (landlord is null)
        {
            return Unauthorized(new ApiResponse<string>("Landlord profile not found."));
        }

        try
        {
            var result = await _reportExecutionService.CompareReportAsync(id, request, userId, landlord.LandlordId);
            return Ok(new ApiResponse<ReportComparisonResultDto>(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPost("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromBody] ReportExportRequestDto request, CancellationToken cancellationToken)
    {
        var (userId, landlord) = await ResolveLandlordContextAsync();
        if (landlord is null)
        {
            return Unauthorized(new ApiResponse<string>("Landlord profile not found."));
        }

        var reportDefinition = await _reportDefinitionRepository.GetByIdAsync(id);
        if (reportDefinition == null || !AllowedCatalogCategories.Contains(reportDefinition.Category))
        {
            return NotFound(new ApiResponse<string>("Report definition not found."));
        }

        try
        {
            ReportResultDto? reportResult = null;
            ReportComparisonResultDto? comparisonResult = null;

            if (request.IncludeComparison)
            {
                var compareRequest = request.ComparisonRequest ?? new ReportComparisonRequestDto
                {
                    Mode = "custom",
                    RunRequest = request.RunRequest ?? new ReportRunRequestDto()
                };

                comparisonResult = await _reportExecutionService.CompareReportAsync(id, compareRequest, userId, landlord.LandlordId);
            }
            else
            {
                reportResult = await _reportExecutionService.RunReportAsync(id, request.RunRequest ?? new ReportRunRequestDto(), userId, landlord.LandlordId);
            }

            if (request.Stream)
            {
                var fileName = string.IsNullOrWhiteSpace(request.FileName)
                    ? $"{reportDefinition.Name}_{Common.Utils.VietnamTime.Now:yyyyMMddHHmmss}.{(request.Format ?? "csv")}" 
                    : request.FileName;

                var fmt = (request.Format ?? "csv").Trim().ToLowerInvariant();
                Response.ContentType = fmt switch
                {
                    "csv" => "text/csv",
                    "xlsx" or "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => "application/octet-stream"
                };

                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                await _reportExportService.StreamExportAsync(
                    reportDefinition.Name,
                    request,
                    reportResult,
                    comparisonResult,
                    Response.Body,
                    cancellationToken);

                return new EmptyResult();
            }

            var export = await _reportExportService.ExportAsync(
                reportDefinition.Name,
                request,
                reportResult,
                comparisonResult,
                cancellationToken);

            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    private async Task<(Guid UserId, Landlord? Landlord)> ResolveLandlordContextAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return (Guid.Empty, null);
        }

        return (userId, await _landlordService.GetByUserIdAsync(userId));
    }

    private static ReportDefinitionResponseDto MapDefinition(ReportDefinition definition)
    {
        return new ReportDefinitionResponseDto
        {
            ReportId = definition.ReportId,
            Name = definition.Name,
            Description = definition.Description,
            Type = definition.Type,
            Category = definition.Category,
            IsActive = definition.IsActive
        };
    }

    private static IEnumerable<string> BuildRowValuesForController(ReportResultRowDto row, IEnumerable<string> headers)
    {
        foreach (var header in headers)
        {
            if (header.StartsWith("dim_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[4..];
                yield return row.Dimensions.TryGetValue(key, out var v) ? (v?.ToString() ?? string.Empty) : string.Empty;
                continue;
            }

            if (header.StartsWith("metric_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[7..];
                yield return row.Metrics.TryGetValue(key, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty;
                continue;
            }

            yield return string.Empty;
        }
    }

    private static string EscapeCsvForController(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
