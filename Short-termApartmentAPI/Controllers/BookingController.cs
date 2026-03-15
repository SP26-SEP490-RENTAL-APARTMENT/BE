//using AutoMapper;
//using BLL.Services.Interfaces;
//using Common.DTOs;
//using DAL.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Short_termApartmentAPI.Middlewares;
//using System.Security.Claims;

//namespace Short_termApartmentAPI.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class BookingController : ControllerBase
//    {
//        private readonly IBookingService _bookingService;
//        private readonly IMapper _mapper;

//        public BookingController(IBookingService bookingService, IMapper mapper)
//        {
//            _bookingService = bookingService;
//            _mapper = mapper;
//        }

//        [HttpGet("{id:guid}")]
//        public async Task<IActionResult> GetById(Guid id)
//        {
//            var result = await _bookingService.GetByIdAsync(id);
//            if (result == null)
//            {
//                return NotFound();
//            }
//            return Ok(_mapper.Map<BookingResponseDto>(result));
//        }

//        [HttpGet]
//        public async Task<IActionResult> GetAll(
//            [FromQuery] int page = 1,
//            [FromQuery] int pageSize = 10,
//            [FromQuery] string? sortBy = null,
//            [FromQuery] string? sortOrder = null,
//            [FromQuery] string? search = null)
//        {
//            var (items, totalCount) = await _bookingService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
//            var mappedItems = _mapper.Map<IEnumerable<BookingResponseDto>>(items);
//            return Ok(new { Items = mappedItems, TotalCount = totalCount });
//        }

//        [Authorize(Roles = "tenant")]
//        [HttpPost]
//        public async Task<IActionResult> Create([FromBody] CreateBookingRequestDto requestDto)
//        {
//            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//            if (!Guid.TryParse(userIdClaim, out var userId))
//            {
//                return Unauthorized(new ApiResponse<string>("Invalid user token."));
//            }

//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var booking = _mapper.Map<Booking>(requestDto);
//            booking.TenantId = userId;
//            var created = await _bookingService.CreateAsync(booking);
//            return CreatedAtAction(nameof(GetById), new { id = created.BookingId }, _mapper.Map<BookingResponseDto>(created));
//        }

//        [HttpPut("{id:guid}")]
//        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBookingRequestDto requestDto)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var booking = await _bookingService.GetByIdAsync(id);
//            if (booking == null)
//            {
//                return NotFound();
//            }

//            _mapper.Map(requestDto, booking);
//            await _bookingService.UpdateAsync(booking);
//            return NoContent();
//        }

//        [HttpDelete("{id:guid}")]
//        public async Task<IActionResult> Delete(Guid id)
//        {
//            await _bookingService.DeleteAsync(id);
//            return NoContent();
//        }
//    }
//}