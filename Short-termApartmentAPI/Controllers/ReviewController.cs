using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Can be restricted further depending on rules (e.g. tenant only)
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IMapper _mapper;

        public ReviewController(IReviewService reviewService, IMapper mapper)
        {
            _reviewService = reviewService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _reviewService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<ReviewResponseDto>(result));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null)
        {
            var (items, totalCount) = await _reviewService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
            var mappedItems = _mapper.Map<IEnumerable<ReviewResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpGet("apartment/{apartmentId:guid}/average-rating")]
        [AllowAnonymous]
        public async Task<IActionResult> GetApartmentAverageRating(Guid apartmentId)
        {
            var (averageRating, totalReviews) = await _reviewService.GetApartmentAverageRatingAsync(apartmentId);
            return Ok(new
            {
                ApartmentId = apartmentId,
                AverageRating = averageRating,
                TotalReviews = totalReviews
            });
        }

        [HttpPost]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> Create([FromBody] CreateReviewRequestDto requestDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _reviewService.ValidateTenantReviewEligibilityAsync(requestDto.BookingId, userId, requestDto.ApartmentId);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var review = _mapper.Map<Review>(requestDto);
            review.ReviewerId = userId; // Attach the authenticated user making the request

            var created = await _reviewService.CreateAsync(review);
            return CreatedAtAction(nameof(GetById), new { id = created.ReviewId }, _mapper.Map<ReviewResponseDto>(created));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewRequestDto requestDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var review = await _reviewService.GetByIdAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            // Optional: Validate that only the Reviewer can update their own review
            if (review.ReviewerId != userId)
            {
                return Forbid();
            }

            _mapper.Map(requestDto, review);
            await _reviewService.UpdateAsync(review);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var review = await _reviewService.GetByIdAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            // Optional: User role check (e.g. only creator or an admin can delete)
            if (review.ReviewerId != userId && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            await _reviewService.DeleteAsync(id);
            return NoContent();
        }
    }
}