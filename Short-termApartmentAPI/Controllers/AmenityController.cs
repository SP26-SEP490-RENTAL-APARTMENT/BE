using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/amenities")]
public sealed class AmenityController(IAmenityService amenityService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var (items, totalCount) = await amenityService.GetAllAmenitiesAsync(
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters);

        return Ok(new ApiResponse<object>(new { items, totalCount, page, pageSize }));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var amenity = await amenityService.GetAmenityByIdAsync(id);
        return amenity is null
            ? NotFound(new ApiResponse<string>("Amenity not found."))
            : Ok(new ApiResponse<AmenityResponseDto>(amenity));
    }

    [HttpPost]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> Create([FromBody] CreateAmenityRequestDto requestDto)
    {
        var created = await amenityService.CreateAmenityAsync(requestDto);
        return CreatedAtAction(nameof(GetById), new { id = created.AmenityId }, new ApiResponse<AmenityResponseDto>(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAmenityRequestDto requestDto)
    {
        try
        {
            await amenityService.UpdateAmenityAsync(id, requestDto);
            return Ok(new ApiResponse<string>(null, "Updated successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await amenityService.DeleteAsync(id);
        return Ok(new ApiResponse<string>(null, "Deleted successfully."));
    }
}
