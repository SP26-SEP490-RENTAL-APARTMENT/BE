using BLL.Services.Implements;
using BLL.Services.Interfaces;
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

    public LandlordReportsController(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IReportExecutionService reportExecutionService,
        IReportExportService reportExportService,
        ILandlordService landlordService)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _reportExecutionService = reportExecutionService;
        _reportExportService = reportExportService;
        _landlordService = landlordService;
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
            var result = await _reportExecutionService.RunReportAsync(id, request, userId, landlord.LandlordId);
            return Ok(new ApiResponse<ReportResultDto>(result));
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
                if (!request.Stream)
                {
                    var compareRequest = request.ComparisonRequest ?? new ReportComparisonRequestDto
                    {
                        Mode = "custom",
                        RunRequest = request.RunRequest ?? new ReportRunRequestDto()
                    };

                    comparisonResult = await _reportExecutionService.CompareReportAsync(id, compareRequest, userId, landlord.LandlordId);
                }
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
                    "pdf" => "application/pdf",
                    "xlsx" or "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => "application/octet-stream"
                };

                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

                if (request.IncludeComparison)
                {
                    var compareRequest = request.ComparisonRequest ?? new ReportComparisonRequestDto
                    {
                        Mode = "custom",
                        RunRequest = request.RunRequest ?? new ReportRunRequestDto()
                    };
                    var currentRun = compareRequest.RunRequest ?? new ReportRunRequestDto();
                    var mode = (compareRequest.Mode ?? "custom").Trim().ToLowerInvariant();

                    var currentFrom = currentRun.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
                    var currentTo = currentRun.To ?? Common.Utils.VietnamTime.Now;

                    ReportRunRequestDto previousRun = new()
                    {
                        SearchTerm = currentRun.SearchTerm,
                        Dimensions = currentRun.Dimensions,
                        Metrics = currentRun.Metrics,
                        Filters = currentRun.Filters
                    };

                    if (mode == "wow")
                    {
                        previousRun.From = currentFrom.AddDays(-7);
                        previousRun.To = currentTo.AddDays(-7);
                    }
                    else if (mode == "mom")
                    {
                        previousRun.From = currentFrom.AddMonths(-1);
                        previousRun.To = currentTo.AddMonths(-1);
                    }
                    else if (mode == "yoy")
                    {
                        previousRun.From = currentFrom.AddYears(-1);
                        previousRun.To = currentTo.AddYears(-1);
                    }
                    else
                    {
                        var duration = currentTo - currentFrom;
                        previousRun.To = currentFrom;
                        previousRun.From = currentFrom - duration;
                    }

                    var compPageSize = Math.Max(1, request.PageSize <= 0 ? 1000 : request.PageSize);
                    var curPage = 1;
                    var prevPage = 1;

                    var curResultPage = await _reportExecutionService.RunReportPageAsync(id, currentRun, curPage, compPageSize, userId, landlord.LandlordId);
                    var prevResultPage = await _reportExecutionService.RunReportPageAsync(id, previousRun, prevPage, compPageSize, userId, landlord.LandlordId);

                    var compDimKeys = new List<string>();
                    var compMetricNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (curResultPage.Rows.Count > 0)
                    {
                        var r = curResultPage.Rows[0];
                        compDimKeys.AddRange(r.Dimensions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
                        foreach (var m in r.Metrics.Keys) { compMetricNames.Add(m); }
                    }
                    if (prevResultPage.Rows.Count > 0)
                    {
                        var r = prevResultPage.Rows[0];
                        foreach (var k in r.Dimensions.Keys) if (!compDimKeys.Contains(k)) compDimKeys.Add(k);
                        foreach (var m in r.Metrics.Keys) compMetricNames.Add(m);
                    }

                    if (compDimKeys.Count == 0 && compMetricNames.Count == 0)
                    {
                        await using var compWriterEmpty = new StreamWriter(Response.Body, Encoding.UTF8, 8192, leaveOpen: true);
                        await compWriterEmpty.WriteLineAsync("No data");
                        await compWriterEmpty.FlushAsync();
                        return new EmptyResult();
                    }

                    var compHeaders = new List<string>();
                    compHeaders.AddRange(compDimKeys.Select(k => $"dim_{k}"));
                    var compMetricList = compMetricNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    compHeaders.AddRange(compMetricList.Select(m => $"current_{m}"));
                    compHeaders.AddRange(compMetricList.Select(m => $"previous_{m}"));
                    compHeaders.AddRange(compMetricList.Select(m => $"delta_{m}"));
                    compHeaders.AddRange(compMetricList.Select(m => $"delta_pct_{m}"));

                    await using var compWriter = new StreamWriter(Response.Body, Encoding.UTF8, 8192, leaveOpen: true);
                    await compWriter.WriteLineAsync(string.Join(",", compHeaders.Select(h => EscapeCsvForController(h)))).ConfigureAwait(false);

                    static string BuildKey(ReportResultRowDto r)
                    {
                        return string.Join("|", r.Dimensions.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => $"{k.Key}:{k.Value}"));
                    }

                    int curIdx = 0, prevIdx = 0;
                    while (true)
                    {
                        ReportResultRowDto? curRow = curIdx < curResultPage.Rows.Count ? curResultPage.Rows[curIdx] : null;
                        ReportResultRowDto? prevRow = prevIdx < prevResultPage.Rows.Count ? prevResultPage.Rows[prevIdx] : null;

                        if (curRow == null && curPage * compPageSize < curResultPage.TotalCount)
                        {
                            curPage++;
                            curResultPage = await _reportExecutionService.RunReportPageAsync(id, currentRun, curPage, compPageSize, userId, landlord.LandlordId);
                            curIdx = 0;
                            curRow = curIdx < curResultPage.Rows.Count ? curResultPage.Rows[curIdx] : null;
                        }

                        if (prevRow == null && prevPage * compPageSize < prevResultPage.TotalCount)
                        {
                            prevPage++;
                            prevResultPage = await _reportExecutionService.RunReportPageAsync(id, previousRun, prevPage, compPageSize, userId, landlord.LandlordId);
                            prevIdx = 0;
                            prevRow = prevIdx < prevResultPage.Rows.Count ? prevResultPage.Rows[prevIdx] : null;
                        }

                        if (curRow == null && prevRow == null) break;

                        var curKey = curRow != null ? BuildKey(curRow) : null;
                        var prevKey = prevRow != null ? BuildKey(prevRow) : null;

                        int cmp = 0;
                        if (curKey == null) cmp = 1;
                        else if (prevKey == null) cmp = -1;
                        else cmp = string.Compare(curKey, prevKey, StringComparison.OrdinalIgnoreCase);

                        ReportResultRowDto? outCur = null;
                        ReportResultRowDto? outPrev = null;

                        if (cmp == 0)
                        {
                            outCur = curRow; outPrev = prevRow; curIdx++; prevIdx++;
                        }
                        else if (cmp < 0)
                        {
                            outCur = curRow; outPrev = null; curIdx++;
                        }
                        else
                        {
                            outCur = null; outPrev = prevRow; prevIdx++;
                        }

                        var values = new List<string>();
                        var dims = outCur?.Dimensions ?? outPrev?.Dimensions ?? new Dictionary<string, object?>();
                        foreach (var key in compDimKeys)
                        {
                            values.Add(dims.TryGetValue(key, out var dv) ? (dv?.ToString() ?? string.Empty) : string.Empty);
                        }

                        foreach (var metric in compMetricList)
                        {
                            var curVal = outCur != null && outCur.Metrics.TryGetValue(metric, out var cv) ? cv : 0m;
                            values.Add(curVal.ToString(CultureInfo.InvariantCulture));
                        }
                        foreach (var metric in compMetricList)
                        {
                            var prevVal = outPrev != null && outPrev.Metrics.TryGetValue(metric, out var pv) ? pv : 0m;
                            values.Add(prevVal.ToString(CultureInfo.InvariantCulture));
                        }
                        foreach (var metric in compMetricList)
                        {
                            var curVal = outCur != null && outCur.Metrics.TryGetValue(metric, out var cv) ? cv : 0m;
                            var prevVal = outPrev != null && outPrev.Metrics.TryGetValue(metric, out var pv) ? pv : 0m;
                            var delta = curVal - prevVal;
                            values.Add(delta.ToString(CultureInfo.InvariantCulture));
                        }
                        foreach (var metric in compMetricList)
                        {
                            var curVal = outCur != null && outCur.Metrics.TryGetValue(metric, out var cv) ? cv : 0m;
                            var prevVal = outPrev != null && outPrev.Metrics.TryGetValue(metric, out var pv) ? pv : 0m;
                            var deltaPct = prevVal == 0m ? 0m : Math.Round((curVal - prevVal) / prevVal * 100m, 2, MidpointRounding.AwayFromZero);
                            values.Add(deltaPct.ToString(CultureInfo.InvariantCulture));
                        }
                        await compWriter.WriteLineAsync(string.Join(",", values.Select(v => EscapeCsvForController(v)))).ConfigureAwait(false);
                    }

                    await compWriter.FlushAsync().ConfigureAwait(false);
                    return new EmptyResult();
                }

                var pageSize = Math.Max(1, request.PageSize <= 0 ? 1000 : request.PageSize);
                var page = 1;

                var firstPage = await _reportExecutionService.RunReportPageAsync(id, request.RunRequest ?? new ReportRunRequestDto(), page, pageSize, userId, landlord.LandlordId);
                var headers = new List<string>();
                if (firstPage.Rows.Count > 0)
                {
                    var firstRow = firstPage.Rows[0];
                    headers.AddRange(firstRow.Dimensions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"dim_{k}"));
                    headers.AddRange(firstRow.Metrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"metric_{k}"));
                }

                await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, 8192, leaveOpen: true);
                await writer.WriteLineAsync(string.Join(",", headers.Select(h => EscapeCsvForController(h)))).ConfigureAwait(false);

                var total = firstPage.TotalCount;
                foreach (var row in firstPage.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync(string.Join(",", BuildRowValuesForController(row, headers))).ConfigureAwait(false);
                }

                while (page * pageSize < total)
                {
                    page++;
                    var p = await _reportExecutionService.RunReportPageAsync(id, request.RunRequest ?? new ReportRunRequestDto(), page, pageSize, userId, landlord.LandlordId);
                    foreach (var row in p.Rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await writer.WriteLineAsync(string.Join(",", BuildRowValuesForController(row, headers))).ConfigureAwait(false);
                    }
                }

                await writer.FlushAsync().ConfigureAwait(false);
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
