using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/check-time-requests")]
    [Authorize]
    public class CheckTimeRequestsController : ControllerBase
    {
        private readonly ICheckTimeRequestService _checkTimeRequestService;
        private readonly IMapper _mapper;

        public CheckTimeRequestsController(
            ICheckTimeRequestService checkTimeRequestService,
            IMapper mapper)
        {
            _checkTimeRequestService = checkTimeRequestService;
            _mapper = mapper;
        }

        /// <summary>
        /// Get a check-time request by ID (guest or landlord only)
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                var result = await _checkTimeRequestService.GetByIdAsync(id, userId);
                if (result == null)
                {
                    return NotFound(new ApiResponse<string>("Check-time request not found."));
                }

                return Ok(new ApiResponse<CheckTimeRequestResponseDto>(result));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error retrieving request: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get all pending check-time requests for a booking (guest or landlord only)
        /// </summary>
        [HttpGet("bookings/{bookingId:guid}")]
        public async Task<IActionResult> GetPendingForBooking(Guid bookingId)
        {
            try
            {
                var result = await _checkTimeRequestService.GetPendingForBookingAsync(bookingId);
                return Ok(new ApiResponse<IEnumerable<CheckTimeRequestResponseDto>>(result));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error retrieving requests: {ex.Message}"));
            }
        }

        /// <summary>
        /// Create a new check-time request (guest only)
        /// </summary>
        [HttpPost("bookings/{bookingId:guid}")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> Create(Guid bookingId, [FromBody] CreateCheckTimeRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                var result = await _checkTimeRequestService.CreateAsync(bookingId, userId, dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<CheckTimeRequestResponseDto>(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error creating request: {ex.Message}"));
            }
        }

        /// <summary>
        /// Landlord counter-offers a different time and/or fee
        /// </summary>
        [HttpPut("{id:guid}/counter")]
        [Authorize(Roles = "landlord,staff")]
        public async Task<IActionResult> CounterOffer(Guid id, [FromBody] CounterCheckTimeDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                var result = await _checkTimeRequestService.CounterOfferAsync(id, userId, dto);
                return Ok(new ApiResponse<CheckTimeRequestResponseDto>(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error countering offer: {ex.Message}"));
            }
        }

        /// <summary>
        /// Guest accepts a counter-offer
        /// </summary>
        [HttpPut("{id:guid}/accept-counter")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> AcceptCounter(Guid id, [FromBody] AcceptCounterDto? dto = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                var result = await _checkTimeRequestService.AcceptCounterAsync(id, userId, dto);
                return Ok(new ApiResponse<CheckTimeRequestResponseDto>(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error accepting counter: {ex.Message}"));
            }
        }

        /// <summary>
        /// Landlord approves a check-time request
        /// </summary>
        [HttpPut("{id:guid}/approve")]
        [Authorize(Roles = "landlord,staff")]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCheckTimeDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                var result = await _checkTimeRequestService.ApproveAsync(id, userId, dto);
                return Ok(new ApiResponse<CheckTimeRequestResponseDto>(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error approving request: {ex.Message}"));
            }
        }

        /// <summary>
        /// Landlord rejects a check-time request
        /// </summary>
        [HttpPut("{id:guid}/reject")]
        [Authorize(Roles = "landlord,staff")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectCheckTimeDto? dto = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                var result = await _checkTimeRequestService.RejectAsync(id, userId, dto);
                return Ok(new ApiResponse<CheckTimeRequestResponseDto>(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>($"Error rejecting request: {ex.Message}"));
            }
        }
    }
}
