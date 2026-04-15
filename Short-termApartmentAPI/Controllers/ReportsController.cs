using AutoMapper;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

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
            var result = await _reportExecutionService.RunReportAsync(id, request, userId);
            return Ok(new ApiResponse<ReportResultDto>(result));
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
}
