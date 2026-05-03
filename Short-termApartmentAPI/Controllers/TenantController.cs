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
    private readonly IWishlistService _wishlistService;
    private readonly IMapper _mapper;

    public TenantController(IBookingService bookingService, IPaymentService paymentService, IWishlistService wishlistService, IMapper mapper)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
        _wishlistService = wishlistService;
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
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Dictionary<string, string>? filters = null)
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
            toDate,
            filters);

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

    [HttpGet("wishlist")]
    public async Task<IActionResult> GetWishlist(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] decimal? priceMin = null,
        [FromQuery] decimal? priceMax = null,
        [FromQuery] Guid? collectionId = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var (items, totalCount) = await _wishlistService.GetTenantWishlistAsync(
                userId, page, pageSize, sortBy, sortOrder, search, priceMin, priceMax, collectionId, filters);

            var response = new WishlistResponseDto
            {
                items = items.ToList(),
                totalCount = totalCount,
                page = page,
                pageSize = pageSize
            };

            return Ok(new ApiResponse<WishlistResponseDto>(response));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>($"Error retrieving wishlist: {ex.Message}"));
        }
    }

    [HttpGet("wishlist/collections/{collectionId:guid}/items")]
    public async Task<IActionResult> GetWishlistByCollectionId(
        Guid collectionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] decimal? priceMin = null,
        [FromQuery] decimal? priceMax = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var (items, totalCount) = await _wishlistService.GetTenantWishlistAsync(
                userId,
                page,
                pageSize,
                sortBy,
                sortOrder,
                search,
                priceMin,
                priceMax,
                collectionId,
                filters);

            var response = new WishlistResponseDto
            {
                items = items.ToList(),
                totalCount = totalCount,
                page = page,
                pageSize = pageSize
            };

            return Ok(new ApiResponse<WishlistResponseDto>(response));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>($"Error retrieving wishlist: {ex.Message}"));
        }
    }

    [HttpPost("wishlist")]
    public async Task<IActionResult> AddToWishlist([FromBody] AddToWishlistRequestDto requestDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var result = await _wishlistService.AddToWishlistAsync(userId, requestDto.apartmentId, requestDto.collectionId, requestDto.notes);
            return Created(string.Empty, new ApiResponse<WishlistItemResponseDto>(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<string>(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>($"Error adding to wishlist: {ex.Message}"));
        }
    }

    [HttpDelete("wishlist/{apartmentId:guid}")]
    public async Task<IActionResult> RemoveFromWishlist(Guid apartmentId, [FromQuery] Guid? collectionId = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            await _wishlistService.RemoveFromWishlistAsync(userId, apartmentId, collectionId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>($"Error removing from wishlist: {ex.Message}"));
        }
    }

    [HttpPut("wishlist/{apartmentId:guid}/favorite")]
    public async Task<IActionResult> ToggleFavorite(Guid apartmentId, [FromBody] ToggleFavoriteRequestDto requestDto, [FromQuery] Guid? collectionId = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var result = await _wishlistService.ToggleFavoriteAsync(userId, apartmentId, requestDto.isFavorite, collectionId);
            return Ok(new ApiResponse<WishlistItemResponseDto>(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>($"Error updating wishlist: {ex.Message}"));
        }
    }

    [HttpGet("wishlist/collections")]
    public async Task<IActionResult> GetWishlistCollections(
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

        var (collections, totalCount) = await _wishlistService.GetCollectionsAsync(
            userId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters);

        var response = new WishlistCollectionListResponseDto
        {
            items = collections.ToList(),
            totalCount = totalCount,
            page = page,
            pageSize = pageSize
        };

        return Ok(new ApiResponse<WishlistCollectionListResponseDto>(response));
    }

    [HttpPost("wishlist/collections")]
    public async Task<IActionResult> CreateWishlistCollection([FromBody] CreateWishlistCollectionRequestDto requestDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var collection = await _wishlistService.CreateCollectionAsync(userId, requestDto.name, requestDto.description);
            return Created(string.Empty, new ApiResponse<WishlistCollectionResponseDto>(collection));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPut("wishlist/collections/{collectionId:guid}")]
    public async Task<IActionResult> UpdateWishlistCollection(Guid collectionId, [FromBody] UpdateWishlistCollectionRequestDto requestDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var collection = await _wishlistService.UpdateCollectionAsync(userId, collectionId, requestDto.name, requestDto.description);
            return Ok(new ApiResponse<WishlistCollectionResponseDto>(collection));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpDelete("wishlist/collections/{collectionId:guid}")]
    public async Task<IActionResult> DeleteWishlistCollection(Guid collectionId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            await _wishlistService.DeleteCollectionAsync(userId, collectionId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPut("wishlist/{apartmentId:guid}/move")]
    public async Task<IActionResult> MoveWishlistItem(Guid apartmentId, [FromBody] MoveWishlistItemRequestDto requestDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        try
        {
            var movedItem = await _wishlistService.MoveWishlistItemAsync(
                userId,
                apartmentId,
                requestDto.sourceCollectionId,
                requestDto.targetCollectionId);

            return Ok(new ApiResponse<WishlistItemResponseDto>(movedItem));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<string>(ex.Message));
        }
    }
}
