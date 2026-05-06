using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

[ApiController]
[Route("api/landlord/apartments/{apartmentId:guid}/pricing")]
[Authorize(Roles = "landlord")]
public class PricingController : ControllerBase
{
    private readonly IApartmentPriceCalendarService _pricingService;
    private readonly IAuthService _authService; // Used for dependency injection and checks

    public PricingController(
        IApartmentPriceCalendarService pricingService,
        IAuthService authService)
    {
        _pricingService = pricingService;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DailyPriceResolutionDto>>> GetCalendarPrices(
        [FromRoute] Guid apartmentId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate)
    {

        // 1. Security Check: Does the acting user own the apartment?
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId, apartmentId))
        {
            return Forbid("You do not have permission to view pricing for this apartment.");
        }

        if (startDate > endDate)
        {
            return BadRequest("Start date cannot be after end date.");
        }

        try
        {
            // Delegate the heavy lifting to the service
            var resolutions = await _pricingService.GetResolvedCalendarAsync(
                apartmentId, startDate, endDate);

            return Ok(resolutions);
        }
        catch (Exception ex)
        {
            // Log the error details (ex)
            Console.WriteLine($"Error in GetCalendarPrices: {ex}");
            return StatusCode(500, "An internal error occurred while resolving prices.");
        }
    }

    // ========================================================
    // ➕ WRITE PATH 1: Manual Single Range Override
    // POST /api/landlord/apartments/{id}/pricing/manual
    // ========================================================
    [HttpPost("manual")]
    public async Task<ActionResult<PricingResultDto>> SetManualPrice(
        [FromRoute] Guid apartmentId,
        [FromBody] ManualPriceRangeDto rangeDto)
    {
        // 1. Basic DTO Validation
        if (rangeDto == null)
        {
            return BadRequest("Manual price range data is required.");
        }
        if (rangeDto.FixedPricePerNight < 0)
        {
            return BadRequest("Fixed price per night must be non-negative.");
        }

        // 2. Security Check
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId, apartmentId))
        {
            return Forbid("You do not have permission to modify pricing for this apartment.");
        }

        try
        {
            // Delegate the complex write logic
            var result = await _pricingService.UpsertManualRangeAsync(
                apartmentId, rangeDto, userId);

            return Ok(result);
        }
        catch (UnauthorizedAccessException uaEx)
        {
            // Catch security exceptions thrown by the service
            return Forbid(uaEx.Message);
        }
        catch (ArgumentException arEx)
        {
            // Catch input validation errors (Start > End, etc.)
            return BadRequest(arEx.Message);
        }
        catch (Exception ex)
        {
            // Log the full error chain, including inner exceptions
            var errorMsg = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"Error in SetManualPrice: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            return StatusCode(500, $"Failed to set pricing range: {errorMsg}");
        }
    }


    // ========================================================
    // 🗑 WRITE PATH 2: Delete Manual Range Override
    // DELETE /api/landlord/apartments/{id}/pricing/manual
    // ========================================================
    [HttpDelete("manual")]
    public async Task<IActionResult> DeleteManualPriceRange(
        [FromRoute] Guid apartmentId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate)
    {
        // 1. Security Check
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId, apartmentId))
        {
            return Forbid("You do not have permission to delete pricing for this apartment.");
        }

        if (startDate > endDate)
        {
            return BadRequest("Start date cannot be after end date.");
        }

        try
        {
            await _pricingService.DeleteManualRangeAsync(
                apartmentId, startDate, endDate, userId);

            // Use 204 No Content for successful deletion
            return NoContent();
        }
        catch (UnauthorizedAccessException uaEx)
        {
            return Forbid(uaEx.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteManualPriceRange: {ex}");
            return StatusCode(500, "Failed to delete pricing range.");
        }
    }

    // ========================================================
    // 🔁 WRITE PATH 3: Bulk Weekday Upsert
    // POST /api/landlord/apartments/{id}/pricing/manual/bulk-weekdays
    // ========================================================
    [HttpPost("manual/bulk-weekdays")]
    public async Task<ActionResult<PricingResultDto>> BulkSetWeekdayPrice(
        [FromRoute] Guid apartmentId,
        [FromBody] BulkPriceUpdateDto updateDto)
    {
        // 1. Basic Input Validation
        if (updateDto == null || updateDto.DaysOfWeek == null || !updateDto.DaysOfWeek.Any())
        {
            return BadRequest("Bulk update requires valid date range and list of days.");
        }

        // 2. Security Check
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        if (!await _authService.IsUserOwnerOrManager(userId, apartmentId))
        {
            return Forbid("You do not have permission to modify bulk pricing.");
        }

        try
        {
            var result = await _pricingService.BulkUpsertWeekdayAsync(
                apartmentId, updateDto, userId);

            return Ok(result);
        }
        catch (UnauthorizedAccessException uaEx)
        {
            return Forbid(uaEx.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in BulkSetWeekdayPrice: {ex}");
            return StatusCode(500, "Failed to process bulk pricing updates.");
        }
    }
}
