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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "admin")]
public sealed class ReportsController : ControllerBase
{
    private readonly ILogger<ReportsController> _logger;
    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IReportExecutionService _reportExecutionService;
    private readonly IReportExportService _reportExportService;
    private readonly IAdminAnalyticsService _adminAnalyticsService;
    private readonly IMapper _mapper;
    private readonly DAL.Data.AppDbContext _dbContext;

    public ReportsController(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IReportExecutionService reportExecutionService,
        IReportExportService reportExportService,
        IAdminAnalyticsService adminAnalyticsService,
        IMapper mapper,
        DAL.Data.AppDbContext dbContext,
        ILogger<ReportsController> logger)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _reportExecutionService = reportExecutionService;
        _reportExportService = reportExportService;
        _adminAnalyticsService = adminAnalyticsService;
        _mapper = mapper;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, totalCount) = await _reportDefinitionRepository.GetAllAsync(
            page,
            pageSize,
            sortBy: nameof(ReportDefinition.CreatedAt),
            sortOrder: "desc",
            search: null,
            filters: null,
            allowedColumns: new[] { "Name", "Category", "Type" });

        foreach (var item in items)
        {
            await _dbContext.Entry(item).Reference(r => r.QueryConfig).LoadAsync();
        }

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

        entity.QueryConfig = new ReportQueryConfig
        {
            ReportId = entity.ReportId,
            DimensionsJson = dto.DimensionsJson,
            MetricsJson = dto.MetricsJson,
            FiltersJson = dto.FiltersJson,
            TimeRangeJson = dto.TimeRangeJson,
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
            // merge saved query config when present, but allow request to override
            request ??= new ReportRunRequestDto();
            var savedConfig = await _dbContext.Set<DAL.Models.ReportQueryConfig>().FindAsync(id);
            if (savedConfig != null)
            {
                try
                {
                    if ((request.Dimensions == null || !request.Dimensions.Any()) && !string.IsNullOrWhiteSpace(savedConfig.DimensionsJson))
                    {
                        request.Dimensions = JsonSerializer.Deserialize<List<ReportDimensionRequestDto>>(savedConfig.DimensionsJson);
                    }

                    if ((request.Metrics == null || !request.Metrics.Any()) && !string.IsNullOrWhiteSpace(savedConfig.MetricsJson))
                    {
                        request.Metrics = JsonSerializer.Deserialize<List<ReportMetricRequestDto>>(savedConfig.MetricsJson);
                    }

                    if ((request.Filters == null || !request.Filters.Any()) && !string.IsNullOrWhiteSpace(savedConfig.FiltersJson))
                    {
                        request.Filters = JsonSerializer.Deserialize<List<ReportFilterRequestDto>>(savedConfig.FiltersJson);
                    }

                    if ((request.From == null && request.To == null) && !string.IsNullOrWhiteSpace(savedConfig.TimeRangeJson))
                    {
                        try
                        {
                            var timeMap = JsonSerializer.Deserialize<Dictionary<string, string>>(savedConfig.TimeRangeJson);
                            if (timeMap != null)
                            {
                                if (timeMap.TryGetValue("from", out var fromStr) && DateTime.TryParse(fromStr, out var fromDt))
                                {
                                    request.From = fromDt;
                                }

                                if (timeMap.TryGetValue("to", out var toStr) && DateTime.TryParse(toStr, out var toDt))
                                {
                                    request.To = toDt;
                                }
                            }
                        }
                        catch { /* ignore time parse errors and proceed with defaults */ }
                    }
                }
                catch { /* ignore config parse errors and proceed with request values */ }
            }

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

    [HttpGet("schema/default")]
    public IActionResult GetDefaultSchema()
    {
        var schema = ReportExecutionService.GetDefaultSchema();
        return Ok(new ApiResponse<ReportSchemaDto>(schema));
    }

    [HttpGet("{id:guid}/config")]
    public async Task<IActionResult> GetConfig(Guid id)
    {
        var config = await _dbContext.Set<DAL.Models.ReportQueryConfig>().FindAsync(id);
        if (config == null)
        {
            return NotFound(new ApiResponse<string>("Report config not found."));
        }

        var dto = new ReportQueryConfigResponseDto
        {
            DimensionsJson = config.DimensionsJson,
            MetricsJson = config.MetricsJson,
            FiltersJson = config.FiltersJson,
            TimeRangeJson = config.TimeRangeJson
        };

        return Ok(new ApiResponse<ReportQueryConfigResponseDto>(dto));
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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ReportDefinitionDto dto)
    {
        var entity = await _reportDefinitionRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return NotFound(new ApiResponse<string>("Report definition not found."));
        }

        var queryConfig = await _dbContext.Set<ReportQueryConfig>().FindAsync(id);

        _logger?.LogInformation(
            "ReportsController.Update called for report {ReportId}. DimensionsJson={DimensionsJson} MetricsJson={MetricsJson}",
            id,
            dto.DimensionsJson ?? "<null>",
            dto.MetricsJson ?? "<null>");

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Type = string.IsNullOrWhiteSpace(dto.Type) ? "custom" : dto.Type;
        entity.Category = dto.Category;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = Common.Utils.VietnamTime.Now;

        // Update the existing query config row directly so edit saves persist reliably.
        if (queryConfig == null)
        {
            entity.QueryConfig = new ReportQueryConfig
            {
                ReportId = entity.ReportId,
                DimensionsJson = dto.DimensionsJson,
                MetricsJson = dto.MetricsJson,
                FiltersJson = dto.FiltersJson,
                TimeRangeJson = dto.TimeRangeJson,
            };
            // ensure new QueryConfig is tracked for insert
            _dbContext.Set<DAL.Models.ReportQueryConfig>().Add(entity.QueryConfig!);
        }
        else
        {
            queryConfig.DimensionsJson = dto.DimensionsJson;
            queryConfig.MetricsJson = dto.MetricsJson;
            queryConfig.FiltersJson = dto.FiltersJson;
            queryConfig.TimeRangeJson = dto.TimeRangeJson;
            entity.QueryConfig = queryConfig;
            // mark as modified because default query tracking behavior is NoTracking
            _dbContext.Entry(queryConfig).State = EntityState.Modified;
        }

        // Also ensure the parent entity is tracked for update
        _dbContext.Set<DAL.Models.ReportDefinition>().Update(entity);

        var changed = await _reportDefinitionRepository.SaveChangesAsync();

        _logger.LogInformation("ReportsController.Update SaveChangesAsync returned {Changes} for report {ReportId}", changed, id);

        try
        {
            var saved = await _dbContext.Set<DAL.Models.ReportQueryConfig>().FindAsync(id);
            _logger.LogInformation("ReportsController.Update post-save config for {ReportId}: DimensionsJson={DimensionsJson} MetricsJson={MetricsJson}", id, saved?.DimensionsJson ?? "<null>", saved?.MetricsJson ?? "<null>");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read back ReportQueryConfig after save for {ReportId}", id);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _reportDefinitionRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return NotFound(new ApiResponse<string>("Report definition not found."));
        }

        // remove entity
        _dbContext.Set<DAL.Models.ReportDefinition>().Remove(entity);
        await _reportDefinitionRepository.SaveChangesAsync();

        return NoContent();
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
