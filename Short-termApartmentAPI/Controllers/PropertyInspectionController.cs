using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PropertyInspectionController : ControllerBase
    {
        private readonly IPropertyInspectionService _propertyInspectionService;
        private readonly IApartmentService _apartmentService;
        private readonly IUserService _userService;
        private readonly IIdentityVerificationService _identityVerificationService;
        private readonly IMapper _mapper;

        public PropertyInspectionController(
            IPropertyInspectionService propertyInspectionService,
            IApartmentService apartmentService,
            IUserService userService,
            IIdentityVerificationService identityVerificationService,
            IMapper mapper)
        {
            _propertyInspectionService = propertyInspectionService;
            _apartmentService = apartmentService;
            _userService = userService;
            _identityVerificationService = identityVerificationService;
            _mapper = mapper;
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out userId);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _propertyInspectionService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<PropertyInspectionResponseDto>(result));
        }

        [HttpGet]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _propertyInspectionService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var mappedItems = _mapper.Map<IEnumerable<PropertyInspectionResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpGet("staff/{staffId:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetByStaffId(
            Guid staffId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _propertyInspectionService.GetByStaffIdAsync(staffId, page, pageSize, sortBy, sortOrder, search, filters);
            var mappedItems = _mapper.Map<IEnumerable<PropertyInspectionResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpGet("staff/me")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> GetMyInspections(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            if (!TryGetCurrentUserId(out var staffId))
            {
                return Unauthorized("Invalid user token.");
            }

            var (items, totalCount) = await _propertyInspectionService.GetByStaffIdAsync(staffId, page, pageSize, sortBy, sortOrder, search, filters);
            var mappedItems = _mapper.Map<IEnumerable<PropertyInspectionResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] CreatePropertyInspectionDto propertyInspectionDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // verify referenced apartment and inspector exist
            var apartment = await _apartmentService.GetByIdAsync(propertyInspectionDto.ApartmentId);
            if (apartment == null)
            {
                return NotFound($"Apartment with id '{propertyInspectionDto.ApartmentId}' not found.");
            }

            var inspector = await _userService.GetByIdAsync(propertyInspectionDto.InspectorId);
            if (inspector == null)
            {
                return NotFound($"Inspector with id '{propertyInspectionDto.InspectorId}' not found.");
            }

            if (!string.Equals(inspector.Role, "staff", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Inspector must be a staff user.");
            }

            if (apartment.Status != "pending_review")
            {
                return BadRequest("Apartment must be in 'pending_review' status to schedule an inspection.");
            }

            try
            {
                await _identityVerificationService.EnsureUserVerifiedForInspectionAsync(apartment.LandlordId);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }

            var propertyInspection = _mapper.Map<PropertyInspection>(propertyInspectionDto);
            propertyInspection.Status = "scheduled";
            var created = await _propertyInspectionService.CreateAsync(propertyInspection);
            return CreatedAtAction(nameof(GetById), new { id = created.InspectionId }, _mapper.Map<PropertyInspectionResponseDto>(created));
        }

        [HttpPost("{id:guid}/start")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> StartInspection(Guid id)
        {
            if (!TryGetCurrentUserId(out var staffId))
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var updated = await _propertyInspectionService.StartInspectionAsync(id, staffId);
                return Ok(_mapper.Map<PropertyInspectionResponseDto>(updated));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:guid}/complete")]
        [Authorize(Roles = "staff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CompleteInspection(Guid id, [FromForm] CompletePropertyInspectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryGetCurrentUserId(out var staffId))
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var updated = await _propertyInspectionService.CompleteInspectionAsync(id, staffId, dto);
                return Ok(_mapper.Map<PropertyInspectionResponseDto>(updated));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:guid}/cancel")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> CancelInspection(Guid id, [FromBody] CancelPropertyInspectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryGetCurrentUserId(out var staffId))
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var updated = await _propertyInspectionService.CancelInspectionAsync(id, staffId, dto.Reason);
                return Ok(_mapper.Map<PropertyInspectionResponseDto>(updated));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:guid}/review")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ReviewInspection(Guid id, [FromBody] ReviewPropertyInspectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryGetCurrentUserId(out var adminId))
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var updated = await _propertyInspectionService.ReviewInspectionAsync(id, adminId, dto.Decision, dto.Reason);
                return Ok(_mapper.Map<PropertyInspectionResponseDto>(updated));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PropertyInspectionRequestDto propertyInspectionDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var propertyInspection = await _propertyInspectionService.GetByIdAsync(id);
            if (propertyInspection == null)
            {
                return NotFound();
            }


            var inspector = await _userService.GetByIdAsync(propertyInspectionDto.InspectorId);
            if (inspector == null)
            {
                return NotFound($"Inspector with id '{propertyInspectionDto.InspectorId}' not found.");
            }

            _mapper.Map(propertyInspectionDto, propertyInspection);
            await _propertyInspectionService.UpdateAsync(propertyInspection);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _propertyInspectionService.DeleteAsync(id);
            return NoContent();
        }
    }
}
