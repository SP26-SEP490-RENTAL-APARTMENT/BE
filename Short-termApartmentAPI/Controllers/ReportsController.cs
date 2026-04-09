using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
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
    private readonly IMapper _mapper;

    public ReportsController(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IReportExecutionService reportExecutionService,
        IMapper mapper)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _reportExecutionService = reportExecutionService;
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

        var dtos = _mapper.Map<IEnumerable<ReportDefinitionDto>>(items);
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

        var createdDto = _mapper.Map<ReportDefinitionDto>(entity);
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
}
