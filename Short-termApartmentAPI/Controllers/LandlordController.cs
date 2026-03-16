using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AutoMapper;
using BLL.Services.Interfaces;
using Short_termApartmentAPI.Middlewares;
using Common.DTOs;
using DAL.Models;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/landlord")]
[Authorize(Roles = "landlord")]
public sealed class LandlordController : ControllerBase
{
    private readonly ILandlordService _landlordService;
    private readonly IMapper _mapper;

    public LandlordController(ILandlordService landlordService, IMapper mapper)
    {
        _landlordService = landlordService;
        _mapper = mapper;
    }
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "landlord ok" });

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        return Ok(new ApiResponse<Landlord>(landlord));
    }

    [HttpGet("apartments")]
    public async Task<IActionResult> GetOwnApartments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var (items, totalCount) = await _landlordService.GetOwnApartmentsAsync(landlord.LandlordId, page, pageSize, sortBy, sortOrder, search);
        var mapped = _mapper.Map<IEnumerable<ApartmentResponseDto>>(items);
        return Ok(new { Items = mapped, TotalCount = totalCount });
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var plan = await _landlordService.GetCurrentSubscriptionAsync(landlord.LandlordId);
        if (plan == null)
            return NotFound(new ApiResponse<string>("No active subscription."));

        return Ok(new ApiResponse<SubscriptionPlanDto>(plan));
    }
}
