using AutoMapper;
using BLL.Services.Interfaces;
using BLL.Services.Implements;
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
[Route("api/reports")]
[Authorize(Roles = "admin")]
public sealed class ReportsController : ControllerBase
{
    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IReportExecutionService _reportExecutionService;
    private readonly IReportExportService _reportExportService;
    private readonly IAdminAnalyticsService _adminAnalyticsService;
    private readonly IMapper _mapper;

    public ReportsController(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IReportExecutionService reportExecutionService,
        IReportExportService reportExportService,
        IAdminAnalyticsService adminAnalyticsService,
        IMapper mapper)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _reportExecutionService = reportExecutionService;
        _reportExportService = reportExportService;
        _adminAnalyticsService = adminAnalyticsService;
        _mapper = mapper;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, totalCount) = await _reportDefinitionRepository.GetAllAsync(
            page,
            pageSize,
            sortBy: null,
            sortOrder: null,
            search: null,
            filters: null,
            allowedColumns: new[] { "Name", "Category", "Type" });

        var dtos = _mapper.Map<IEnumerable<ReportDefinitionResponseDto>>(items);
        return Ok(new { Items = dtos, TotalCount = totalCount });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReportDefinitionDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var entity = new ReportDefinition
        {
            ReportId = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "custom" : dto.Type,
            Category = dto.Category,
            IsActive = dto.IsActive,
            CreatedBy = userId,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };

        await _reportDefinitionRepository.AddAsync(entity);
        await _reportDefinitionRepository.SaveChangesAsync();

        var createdDto = _mapper.Map<ReportDefinitionResponseDto>(entity);
        return CreatedAtAction(nameof(GetCatalog), new { page = 1, pageSize = 1 }, createdDto);
    }

    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, [FromBody] ReportRunRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Max(1, request.PageSize);
            var result = await _reportExecutionService.RunReportPageAsync(id, request, page, pageSize, userId);
            return Ok(new ApiResponse<ReportResultPageDto>(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<IActionResult> GetSchema(Guid id)
    {
        var definition = await _reportDefinitionRepository.GetByIdAsync(id);
        if (definition == null)
        {
            return NotFound(new ApiResponse<string>("Report definition not found."));
        }

        var schema = ReportExecutionService.GetDefaultSchema();
        return Ok(new ApiResponse<ReportSchemaDto>(schema));
    }

    [HttpPost("{id:guid}/compare")]
    public async Task<IActionResult> Compare(Guid id, [FromBody] ReportComparisonRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var result = await _reportExecutionService.CompareReportAsync(id, request, userId);
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
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var reportDefinition = await _reportDefinitionRepository.GetByIdAsync(id);
        if (reportDefinition == null)
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

                comparisonResult = await _reportExecutionService.CompareReportAsync(id, compareRequest, userId);
            }
            else
            {
                reportResult = await _reportExecutionService.RunReportAsync(id, request.RunRequest ?? new ReportRunRequestDto(), userId);
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

    [HttpGet("streaming/snapshot")]
    public async Task<IActionResult> GetStreamingSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await _adminAnalyticsService.GetSnapshotAsync(cancellationToken);
        return Ok(new ApiResponse<AdminAnalyticsSnapshotDto>(snapshot));
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
