using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/smart-pricing")]
public sealed class SmartPricingController : ControllerBase
{
    private readonly ISmartPricingHistoryService _smartPricingService;
    private readonly IApartmentService _apartmentService;
    private readonly ILandlordService _landlordService;
    private readonly IMapper _mapper;

    public SmartPricingController(
        ISmartPricingHistoryService smartPricingService,
        IApartmentService apartmentService,
        ILandlordService landlordService,
        IMapper mapper)
    {
        _smartPricingService = smartPricingService;
        _apartmentService = apartmentService;
        _landlordService = landlordService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get a smart price suggestion for an apartment on a specific date.
    /// Based on occupancy rate and dynamic pricing algorithm.
    /// </summary>
    [HttpPost("suggest")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> SuggestPrice([FromBody] SuggestPriceDto dto)
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

            var apartment = await _apartmentService.GetByIdAsync(dto.ApartmentId);
            if (apartment == null || apartment.LandlordId != landlord.LandlordId)
            {
                return NotFound(new ApiResponse<string>("Apartment not found or you don't have permission."));
            }

            var suggestion = await _smartPricingService.SuggestPriceAsync(
                dto.ApartmentId,
                dto.Date,
                dto.OccupancyRate);

            var response = _mapper.Map<SmartPricingResponseDto>(suggestion);
            return Ok(new ApiResponse<SmartPricingResponseDto>(response, "Price suggestion generated successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    /// <summary>
    /// Landlord accepts a price suggestion or provides an override price.
    /// Once accepted, this price becomes the official listing price.
    /// </summary>
    [HttpPost("{pricingId:guid}/accept")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> AcceptPriceSuggestion(Guid pricingId, [FromBody] AcceptPriceSuggestionDto dto)
    {
        try
        {
            var pricing = await _smartPricingService.GetByIdAsync(pricingId);
            if (pricing == null)
            {
                return NotFound(new ApiResponse<string>("Price suggestion not found."));
            }

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

            var apartment = await _apartmentService.GetByIdAsync(pricing.ApartmentId);
            if (apartment == null || apartment.LandlordId != landlord.LandlordId)
            {
                return NotFound(new ApiResponse<string>("Permission denied."));
            }

            var accepted = await _smartPricingService.AcceptPriceSuggestionAsync(pricingId, dto.OverridePrice);
            var response = _mapper.Map<SmartPricingResponseDto>(accepted);
            return Ok(new ApiResponse<SmartPricingResponseDto>(response, "Price suggestion accepted successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    /// <summary>
    /// Admin gets all smart pricing suggestions.
    /// </summary>
    [HttpGet("admin/suggestions")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllSuggestionsForAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var (items, totalCount) = await _smartPricingService.GetAllSuggestionsAsync(
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters);

        var response = new SmartPricingListResponseDto
        {
            Items = _mapper.Map<IEnumerable<SmartPricingResponseDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(new ApiResponse<SmartPricingListResponseDto>(response));
    }

    /// <summary>
    /// Landlord gets smart pricing suggestions for their own apartments.
    /// </summary>
    [HttpGet("landlord/suggestions")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> GetOwnSuggestionsForLandlord(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] Dictionary<string, string>? filters = null)
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

        var (items, totalCount) = await _smartPricingService.GetSuggestionsForLandlordAsync(
            landlord.LandlordId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters);

        var response = new SmartPricingListResponseDto
        {
            Items = _mapper.Map<IEnumerable<SmartPricingResponseDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(new ApiResponse<SmartPricingListResponseDto>(response));
    }
}
