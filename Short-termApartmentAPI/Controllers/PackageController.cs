using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PackageController : ControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IApartmentService _apartmentService;
        private readonly ILandlordService _landlordService;
        private readonly IMapper _mapper;

        public PackageController(
            IPackageService packageService,
            IApartmentService apartmentService,
            ILandlordService landlordService,
            IMapper mapper)
        {
            _packageService = packageService;
            _apartmentService = apartmentService;
            _landlordService = landlordService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _packageService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<PackageResponseDto>(result));
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
            // use service method that includes package items
            var (items, totalCount) = await _packageService.GetAllWithDetailsAsync(page, pageSize, sortBy, sortOrder, search);
            var mappedItems = _mapper.Map<IEnumerable<PackageResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpPost]
        [Authorize(Roles = "admin,landlord")]
        public async Task<IActionResult> Create([FromBody] PackageRequestDto packageDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var package = _mapper.Map<Package>(packageDto);
            var created = await _packageService.CreateAsync(package);
            return CreatedAtAction(nameof(GetById), new { id = created.PackageId }, _mapper.Map<PackageResponseDto>(created));
        }

        [HttpPost("{packageId:guid}/items")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AdminAddItems(Guid packageId, [FromBody] List<Guid> packageItemIds)
        {
            if (packageItemIds == null || !packageItemIds.Any())
                return BadRequest("No package item ids provided.");

            try
            {
                await _packageService.AddItemsAsync(packageId, packageItemIds);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id:guid}/items/{itemId:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AdminRemoveItem(Guid packageId, Guid packageItemId)
        {
            try
            {
                await _packageService.RemoveItemAsync(packageId, packageItemId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PackageRequestDto packageDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var package = await _packageService.GetByIdAsync(id);
            if (package == null)
            {
                return NotFound();
            }

            _mapper.Map(packageDto, package);
            await _packageService.UpdateAsync(package);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _packageService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Landlord removes a package from their apartment.
        /// </summary>
        [HttpDelete("apartments/{apartmentId:guid}/packages/{packageId:guid}")]
        [Authorize(Roles = "landlord")]
        public async Task<IActionResult> LandlordRemovePackageFromApartment(Guid apartmentId, Guid packageId)
        {
            try
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

                var apartment = await _apartmentService.GetByIdAsync(apartmentId);
                if (apartment == null || apartment.LandlordId != landlord.LandlordId)
                {
                    return NotFound(new ApiResponse<string>("Apartment not found or permission denied."));
                }

                await _packageService.RemoveItemAsync(packageId, apartmentId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }
    }
}