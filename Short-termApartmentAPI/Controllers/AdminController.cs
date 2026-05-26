using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BLL.Services.Interfaces;
using BLL.Services.Implements;
using Common.DTOs;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IPricingPolicyService _pricingPolicyService;
    private readonly IBookingService _bookingService;
    private readonly PricingPolicyMigrationService _migrationService;

    public AdminController(
        IPricingPolicyService pricingPolicyService,
        IBookingService bookingService,
        PricingPolicyMigrationService migrationService)
    {
        _pricingPolicyService = pricingPolicyService;
        _bookingService = bookingService;
        _migrationService = migrationService;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "admin ok" });

    [HttpGet("pricing/templates")]
    public async Task<ActionResult<IEnumerable<PricingRuleTemplateResponseDto>>> GetPricingTemplates()
    {
        var templates = await _pricingPolicyService.GetTemplatesAsync();
        return Ok(templates);
    }

    [HttpPost("pricing/templates")]
    public async Task<ActionResult<PricingRuleTemplateResponseDto>> CreatePricingTemplate([FromBody] CreatePricingRuleTemplateDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new { message = "Invalid admin token." });
        }

        try
        {
            var template = await _pricingPolicyService.CreateTemplateAsync(dto, adminId.Value);
            return Ok(template);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("pricing/templates/{templateId:guid}")]
    public async Task<ActionResult<PricingRuleTemplateResponseDto>> UpdatePricingTemplate(
        [FromRoute] Guid templateId,
        [FromBody] CreatePricingRuleTemplateDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new { message = "Invalid admin token." });
        }

        try
        {
            var template = await _pricingPolicyService.UpdateTemplateAsync(templateId, dto, adminId.Value);
            return Ok(template);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("pricing/templates/{templateId:guid}/status")]
    public async Task<ActionResult<PricingRuleTemplateResponseDto>> SetPricingTemplateStatus(
        [FromRoute] Guid templateId,
        [FromQuery] bool active)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new { message = "Invalid admin token." });
        }

        try
        {
            var template = await _pricingPolicyService.SetTemplateStatusAsync(templateId, active, adminId.Value);
            return Ok(template);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("bookings/reported")]
    public async Task<IActionResult> GetReportedBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var (items, totalCount) = await _bookingService.GetReportedBookingsAsync(
                page, pageSize, sortBy, sortOrder, search, fromDate, toDate);

            return Ok(new
            {
                data = items,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalCount = totalCount,
                    totalPages = (totalCount + pageSize - 1) / pageSize
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("bookings/disputes")]
    public async Task<IActionResult> GetDisputedBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var (items, totalCount) = await _bookingService.GetDisputedBookingsAsync(
                page, pageSize, sortBy, sortOrder, search, fromDate, toDate);

            return Ok(new
            {
                data = items,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalCount = totalCount,
                    totalPages = (totalCount + pageSize - 1) / pageSize
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("pricing/migrate-policies")]
    public async Task<IActionResult> MigratePricingPolicies()
    {
        try
        {
            var result = await _migrationService.MigrateAllPricingPoliciesAsync();
            return Ok(new
            {
                message = "Pricing policy migration completed.",
                totalApplications = result.TotalApplications,
                successfullyMigrated = result.SuccessfullyMigrated,
                failed = result.Failed,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid? GetAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var adminId) ? adminId : null;
    }
}
