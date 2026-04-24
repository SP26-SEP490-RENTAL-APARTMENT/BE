using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/apartments")]
public sealed class ApartmentsController : ControllerBase
{
    private readonly IApartmentService _apartmentService;
    private readonly ILandlordService _landlordService;
    private readonly IBookingService _bookingService;
    private readonly ISmartPricingHistoryService _smartPricingHistoryService;
    private readonly IMapper _mapper;

    public ApartmentsController(
        IApartmentService apartmentService,
        ILandlordService landlordService,
        IBookingService bookingService,
        ISmartPricingHistoryService smartPricingHistoryService,
        IMapper mapper)
    {
        _apartmentService = apartmentService;
        _landlordService = landlordService;
        _bookingService = bookingService;
        _smartPricingHistoryService = smartPricingHistoryService;
        _mapper = mapper;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPublic(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
    {
        var (items, totalCount) = await _apartmentService.GetAllPublicAsync(page, pageSize, sortBy, sortOrder, search, filters);
        var mappedItems = _mapper.Map<IEnumerable<ApartmentResponseDto>>(items);
        return Ok(new { Items = mappedItems, TotalCount = totalCount });
    }

    [HttpGet]
    [Authorize(Roles = "admin,staff,landlord")]
    public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
    {
        var (items, totalCount) = await _apartmentService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
        var mappedItems = _mapper.Map<IEnumerable<ApartmentResponseDto>>(items);
        return Ok(new { Items = mappedItems, TotalCount = totalCount });
    }

    /// <summary>
    /// Admin gets apartments that are pending review.
    /// </summary>
    [HttpGet("pending-review")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetPendingReview(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
    {
        var effectiveFilters = filters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        effectiveFilters["status"] = "pending_review";

        var (items, totalCount) = await _apartmentService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, effectiveFilters);
        var mappedItems = items.Select(apartment =>
        {
            var dto = _mapper.Map<ApartmentResponseDto>(apartment);
            dto.InspectionStatus = apartment.PropertyInspections
                .OrderByDescending(i => i.ApprovedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.CompletedDate ?? DateOnly.MinValue)
                .ThenByDescending(i => i.ScheduledDate ?? DateOnly.MinValue)
                .Select(i => i.Status)
                .FirstOrDefault();
            return dto;
        });
        return Ok(new { Items = mappedItems, TotalCount = totalCount });
    }

    /// <summary>
    /// Admin gets apartments that are pending review for a specific landlord.
    /// </summary>
    [HttpGet("pending-review/landlord/{landlordId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetPendingReviewByLandlordId(
            Guid landlordId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
    {
        var effectiveFilters = filters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        effectiveFilters["status"] = "pending_review";
        effectiveFilters["landlordId"] = landlordId.ToString();

        var (items, totalCount) = await _apartmentService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, effectiveFilters);
        var mappedItems = items.Select(apartment =>
        {
            var dto = _mapper.Map<ApartmentResponseDto>(apartment);
            dto.InspectionStatus = apartment.PropertyInspections
                .OrderByDescending(i => i.ApprovedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.CompletedDate ?? DateOnly.MinValue)
                .ThenByDescending(i => i.ScheduledDate ?? DateOnly.MinValue)
                .Select(i => i.Status)
                .FirstOrDefault();
            return dto;
        });
        return Ok(new { Items = mappedItems, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var apartment = await _apartmentService.GetApartmentWithDetailsAsync(id);
        if (apartment == null)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        return Ok(new ApiResponse<ApartmentResponseDto>(apartment));
    }

    [HttpPost]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> Create([FromForm] CreateApartmentRequestDto requestDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var landlordId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var created = await _apartmentService.CreateApartmentWithPhotosAsync(requestDto, landlordId);
            return CreatedAtAction(nameof(GetById), new { id = created.ApartmentId }, new ApiResponse<CreateApartmentResponseDto>(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApartmentRequestDto requestDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var apartment = await _apartmentService.GetByIdAsync(id);
        if (apartment == null)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        // Ensure the authenticated landlord owns this apartment
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null || apartment.LandlordId != landlord.LandlordId)
        {
            // Hide existence to unauthorized landlords
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        _mapper.Map(requestDto, apartment);
        await _apartmentService.UpdateAsync(apartment);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var apartment = await _apartmentService.GetByIdAsync(id);
        if (apartment == null)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null || apartment.LandlordId != landlord.LandlordId)
        {
            // Hide existence to unauthorized landlords
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        await _apartmentService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/amenities")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> AddAmenities(Guid id, [FromBody] List<Guid> amenityIds)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        var apartment = await _apartmentService.GetByIdAsync(id);
        if (apartment == null || apartment.LandlordId != landlord.LandlordId)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        try
        {
            await _apartmentService.AddAmenitiesAsync(id, amenityIds);

            var updatedApartment = await _apartmentService.GetApartmentWithDetailsAsync(id);
            if (updatedApartment == null)
            {
                return NotFound(new ApiResponse<string>("Apartment not found."));
            }

            return Ok(new ApiResponse<string>("Amenities added successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpDelete("{id:guid}/amenities")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> RemoveAmenities(Guid id, [FromBody] List<Guid> amenityIds)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        var apartment = await _apartmentService.GetByIdAsync(id);
        if (apartment == null || apartment.LandlordId != landlord.LandlordId)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        try
        {
            await _apartmentService.RemoveAmenitiesAsync(id, amenityIds);

            var updatedApartment = await _apartmentService.GetApartmentWithDetailsAsync(id);
            if (updatedApartment == null)
            {
                return NotFound(new ApiResponse<string>("Apartment not found."));
            }

            return Ok(new ApiResponse<string>("Amenities removed successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPut("{id:guid}/photos")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> UpdatePhotos(Guid id, [FromForm] List<Microsoft.AspNetCore.Http.IFormFile> photos)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        var apartment = await _apartmentService.GetByIdAsync(id);
        if (apartment == null || apartment.LandlordId != landlord.LandlordId)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        try
        {
            await _apartmentService.UpdateApartmentPhotosAsync(id, photos);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    /// <summary>
    /// Landlord submits apartment for admin review. Validates ownership and listing details.
    /// Transitions apartment from 'draft' to 'pending_review'.
    /// </summary>
    [HttpPost("{id:guid}/submit-for-review")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> SubmitForReview(Guid id, [FromBody] SubmitForReviewDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var landlord = await _landlordService.GetByUserIdAsync(userId);
            if (landlord == null)
            {
                return NotFound(new ApiResponse<string>("Landlord profile not found."));
            }

            var apartment = await _apartmentService.GetByIdAsync(id);
            if (apartment == null || apartment.LandlordId != landlord.LandlordId)
            {
                return NotFound(new ApiResponse<string>("Apartment not found."));
            }

            var updated = await _apartmentService.SubmitForReviewAsync(id, landlord.LandlordId, dto);
            var response = _mapper.Map<ApartmentResponseDto>(updated);
            var hasAcceptedRecommendation = await _smartPricingHistoryService.HasAcceptedSuggestionAsync(id);
            var message = hasAcceptedRecommendation
                ? "Apartment submitted for review successfully."
                : "Apartment submitted for review successfully. Warning: no smart pricing recommendation has been accepted yet.";

            return Ok(new ApiResponse<ApartmentResponseDto>(response, message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    /// <summary>
    /// Admin approves or rejects a pending_review apartment.
    /// Transitions from 'pending_review' to 'posted' (approved) or 'blocked' (rejected).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ApproveListing(Guid id, [FromBody] ApproveListingDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var adminId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var apartment = await _apartmentService.GetByIdAsync(id);
            if (apartment == null)
            {
                return NotFound(new ApiResponse<string>("Apartment not found."));
            }

            var updated = await _apartmentService.ApproveListingAsync(id, adminId, dto);
            var response = _mapper.Map<ApartmentResponseDto>(updated);
            string message = dto.Approved
                ? "Apartment approved and posted successfully."
                : "Apartment rejected.";
            return Ok(new ApiResponse<ApartmentResponseDto>(response, message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    /// <summary>
    /// Admin/Staff/Landlord unpublishes a posted apartment, transitioning it back to draft status.
    /// Landlords can only unpublish their own apartments; admin and staff can unpublish any apartment.
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Roles = "admin,staff,landlord")]
    public async Task<IActionResult> UnpublishApartment(Guid id, [FromBody] UnpublishApartmentDto? dto = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var apartment = await _apartmentService.GetByIdAsync(id);
            if (apartment == null)
            {
                return NotFound(new ApiResponse<string>("Apartment not found."));
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            // For landlords, verify ownership
            if (userRole == "landlord")
            {
                var landlord = await _landlordService.GetByUserIdAsync(userId);
                if (landlord == null || apartment.LandlordId != landlord.LandlordId)
                {
                    return NotFound(new ApiResponse<string>("Apartment not found."));
                }
            }

            var updated = await _apartmentService.UnpublishApartmentAsync(id, userId, dto?.Reason);
            var response = _mapper.Map<ApartmentResponseDto>(updated);
            return Ok(new ApiResponse<ApartmentResponseDto>(response, "Apartment unpublished successfully and returned to draft status."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    /// <summary>
    /// Gets the availability calendar for an apartment showing available and unavailable date ranges.
    /// Default range: next 90 days from today. Can be customized via query parameters.
    /// Anonymous users see availability only; landlord/staff see booking details for unavailable periods.
    /// All dates returned in UTC.
    /// </summary>
    [HttpGet("{id:guid}/availability-calendar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailabilityCalendar(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Extract user info if authenticated
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var rolesClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            Guid? requesterId = null;
            string? requesterRole = null;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                requesterId = userId;
                requesterRole = rolesClaim;
            }

            var calendar = await _bookingService.GetAvailabilityCalendarAsync(
                id,
                startDate,
                endDate,
                requesterId,
                requesterRole);

            return Ok(new ApiResponse<AvailabilityCalendarResponseDto>(calendar, "Availability calendar retrieved successfully."));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPost("{id:guid}/availability")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> SetAvailability(Guid id, [FromBody] SetApartmentAvailabilityRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var landlordUserId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(landlordUserId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        try
        {
            var result = await _bookingService.SetApartmentAvailabilityAsync(id, landlord.LandlordId, dto);
            return Ok(new ApiResponse<SetApartmentAvailabilityResponseDto>(result, "Availability updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpDelete("{id:guid}/availability")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> RemoveAvailability(Guid id, [FromBody] RemoveApartmentAvailabilityRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var landlordUserId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(landlordUserId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        try
        {
            var result = await _bookingService.RemoveApartmentAvailabilityAsync(id, landlord.LandlordId, dto);
            return Ok(new ApiResponse<RemoveApartmentAvailabilityResponseDto>(result, "Availability removed successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }
}
