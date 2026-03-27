using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/tenant")]
[Authorize(Roles = "tenant")]
public sealed class TenantController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly IPaymentService _paymentService;
    private readonly IMapper _mapper;

    public TenantController(IBookingService bookingService, IPaymentService paymentService, IMapper mapper)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
        _mapper = mapper;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "tenant ok" });

    [HttpGet("bookings/history")]
    public async Task<IActionResult> GetBookingHistory(
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

        filters ??= new Dictionary<string, string>();
        filters["TenantId"] = userId.ToString();

        var (items, totalCount) = await _bookingService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
        var dtos = _mapper.Map<IEnumerable<BookingResponseDto>>(items);
        return Ok(new { Items = dtos, TotalCount = totalCount });
    }

    [HttpGet("bookings/{id:guid}")]
    public async Task<IActionResult> GetBookingById(Guid id)
    {
        var booking = await _bookingService.GetByIdAsync(id);
        if (booking == null)
        {
            return NotFound(new ApiResponse<string>("Booking not found."));
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        if (booking.TenantId != userId)
        {
            return NotFound(new ApiResponse<string>("Booking not found."));
        }

        var dto = _mapper.Map<BookingResponseDto>(booking);
        return Ok(new ApiResponse<BookingResponseDto>(dto));
    }

    [HttpGet("payments/history")]
    public async Task<IActionResult> GetPaymentHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var (items, totalCount) = await _paymentService.GetTenantPaymentsAsync(
            userId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            fromDate,
            toDate);

        var dtos = _mapper.Map<IEnumerable<PaymentHistoryDto>>(items);
        return Ok(new { Items = dtos, TotalCount = totalCount });
    }

    [HttpGet("payments/{id:guid}")]
    public async Task<IActionResult> GetPaymentById(Guid id)
    {
        var payment = await _paymentService.GetByIdAsync(id);
        if (payment == null)
        {
            return NotFound(new ApiResponse<string>("Payment not found."));
        }
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        if (payment.RelatedEntityType == "booking" && payment.RelatedEntityId.HasValue)
        {
            var booking = await _bookingService.GetByIdAsync(payment.RelatedEntityId.Value);
            if (booking == null || booking.TenantId != userId)
            {
                return NotFound(new ApiResponse<string>("Payment not found."));
            }
        }
        else
        {
            return NotFound(new ApiResponse<string>("Payment not found."));
        }

        var dto = _mapper.Map<PaymentHistoryDto>(payment);
        return Ok(new ApiResponse<PaymentHistoryDto>(dto));
    }
}
