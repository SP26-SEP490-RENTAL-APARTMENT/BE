using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BLL.Services.Interfaces;
using Common.DTOs;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IPricingPolicyService _pricingPolicyService;

    public AdminController(IPricingPolicyService pricingPolicyService)
    {
        _pricingPolicyService = pricingPolicyService;
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

    private Guid? GetAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var adminId) ? adminId : null;
    }
}
