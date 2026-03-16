using AutoMapper;
using System.Security.Claims;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/apartments")]
public sealed class ApartmentsController : ControllerBase
{
    private readonly IApartmentService _apartmentService;
    private readonly ILandlordService _landlordService;
    private readonly IMapper _mapper;

    public ApartmentsController(IApartmentService apartmentService, ILandlordService landlordService, IMapper mapper)
    {
        _apartmentService = apartmentService;
        _landlordService = landlordService;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null)
    {
        var (items, totalCount) = await _apartmentService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
        var mappedItems = _mapper.Map<IEnumerable<ApartmentResponseDto>>(items);
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
    public async Task<IActionResult> Delete(Guid id)
    {
        await _apartmentService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/amenities")]
    [Authorize(Roles = "landlord")]
    public async Task<IActionResult> AddAmenities(Guid id, [FromBody] List<Guid> amenityIds)
    {
        try
        {
            await _apartmentService.AddAmenitiesAsync(id, amenityIds);
            return Ok("Amenity Added!");
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiResponse<string>(ex.Message));
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
}
