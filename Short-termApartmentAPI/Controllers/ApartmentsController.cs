using System.Security.Claims;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/apartments")]
public sealed class ApartmentsController(IApartmentService apartmentService) : ControllerBase
{
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

            var created = await apartmentService.CreateApartmentWithPhotosAsync(requestDto, landlordId);
            return CreatedAtAction(nameof(GetById), new { id = created.ApartmentId }, new ApiResponse<CreateApartmentResponseDto>(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var apartment = await apartmentService.GetApartmentWithDetailsAsync(id);
        if (apartment == null)
        {
            return NotFound(new ApiResponse<string>("Apartment not found."));
        }

        return Ok(new ApiResponse<ApartmentResponseDto>(apartment));
    }
}
