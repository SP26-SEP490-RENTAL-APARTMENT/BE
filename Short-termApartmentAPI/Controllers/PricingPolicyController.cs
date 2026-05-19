using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/landlord/apartments/{apartmentId:guid}/pricing/policies")]
[Authorize(Roles = "landlord")]
public sealed class PricingPolicyController : ControllerBase
{
    private readonly IPricingPolicyService _pricingPolicyService;
    private readonly IAuthService _authService;

    public PricingPolicyController(IPricingPolicyService pricingPolicyService, IAuthService authService)
    {
        _pricingPolicyService = pricingPolicyService;
        _authService = authService;
    }

    [HttpPost]
    public async Task<ActionResult<ApartmentPricingPolicyApplicationResponseDto>> ApplyPolicy(
        [FromRoute] Guid apartmentId,
        [FromBody] CreateApartmentPricingPolicyApplicationDto dto)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId.Value, apartmentId))
        {
            return Forbid("You do not have permission to modify pricing for this apartment.");
        }

        try
        {
            var result = await _pricingPolicyService.ApplyTemplateAsync(apartmentId, dto, userId.Value);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<AvailableTemplatesForApartmentDto>> GetAvailableTemplates(
        [FromRoute] Guid apartmentId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId.Value, apartmentId))
        {
            return Forbid("You do not have permission to view templates for this apartment.");
        }

        if (startDate > endDate)
            return BadRequest("Start date cannot be after end date.");

        try
        {
            var result = await _pricingPolicyService.GetAvailableTemplatesForApartmentAsync(apartmentId, startDate, endDate);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("/api/landlord/pricing/templates")]
    public async Task<ActionResult<IEnumerable<PricingRuleTemplateResponseDto>>> GetAllTemplatesForLandlord()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        try
        {
            var templates = await _pricingPolicyService.GetTemplatesAsync();
            return Ok(templates);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("templates/available")]
    public async Task<ActionResult<AvailableTemplatesForApartmentDto>> GetAvailableTemplatesSimple(
        [FromRoute] Guid apartmentId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId.Value, apartmentId))
        {
            return Forbid("You do not have permission to view templates for this apartment.");
        }

        var resolvedStartDate = startDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date);
        var resolvedEndDate = endDate ?? resolvedStartDate;

        if (resolvedStartDate > resolvedEndDate)
        {
            return BadRequest("Start date cannot be after end date.");
        }

        try
        {
            var result = await _pricingPolicyService.GetAvailableTemplatesForApartmentAsync(apartmentId, resolvedStartDate, resolvedEndDate);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{applicationId:guid}/enabled")]
    public async Task<ActionResult<ApartmentPricingPolicyApplicationResponseDto>> SetApplicationStatus(
        [FromRoute] Guid apartmentId,
        [FromRoute] Guid applicationId,
        [FromQuery] bool enabled)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId.Value, apartmentId))
        {
            return Forbid("You do not have permission to modify pricing for this apartment.");
        }

        try
        {
            var result = await _pricingPolicyService.SetApplicationStatusAsync(apartmentId, applicationId, enabled, userId.Value);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPatch("{applicationId:guid}/overrides")]
    public async Task<ActionResult<ApartmentPricingPolicyApplicationResponseDto>> UpdateApplicationOverrides(
        [FromRoute] Guid apartmentId,
        [FromRoute] Guid applicationId,
        [FromBody] UpdateApartmentPricingPolicyApplicationOverridesDto dto)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId.Value, apartmentId))
        {
            return Forbid("You do not have permission to modify pricing for this apartment.");
        }

        try
        {
            var result = await _pricingPolicyService.UpdateApplicationOverridesAsync(apartmentId, applicationId, dto, userId.Value);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}