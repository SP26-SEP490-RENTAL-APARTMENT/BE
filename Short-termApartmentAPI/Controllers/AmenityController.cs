using BLL.Services.Interfaces;
using DAL.Models;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AmenityController : ControllerBase
    {
        private readonly IAmenityService _amenityService;

        public AmenityController(IAmenityService amenityService)
        {
            _amenityService = amenityService;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _amenityService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null)
        {
            var (items, totalCount) = await _amenityService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
            return Ok(new { Items = items, TotalCount = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Amenity amenity)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var created = await _amenityService.CreateAsync(amenity);
            return CreatedAtAction(nameof(GetById), new { id = created.AmenityId }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Amenity amenity)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            amenity.AmenityId = id;
            await _amenityService.UpdateAsync(amenity);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _amenityService.DeleteAsync(id);
            return NoContent();
        }
    }
}