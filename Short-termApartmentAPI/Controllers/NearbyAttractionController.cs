using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,staff")]
    public class NearbyAttractionController : ControllerBase
    {
        private readonly INearbyAttractionService _nearbyAttractionService;
        private readonly IMapper _mapper;

        public NearbyAttractionController(INearbyAttractionService nearbyAttractionService, IMapper mapper)
        {
            _nearbyAttractionService = nearbyAttractionService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _nearbyAttractionService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<NearbyAttractionResponseDto>(result));
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
            var (items, totalCount) = await _nearbyAttractionService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
            var mappedItems = _mapper.Map<IEnumerable<NearbyAttractionResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNearbyAttractionRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var nearbyAttraction = _mapper.Map<NearbyAttraction>(requestDto);
            
            var created = await _nearbyAttractionService.CreateAsync(nearbyAttraction);
            return CreatedAtAction(nameof(GetById), new { id = created.AttractionId }, _mapper.Map<NearbyAttractionResponseDto>(created));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNearbyAttractionRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var nearbyAttraction = await _nearbyAttractionService.GetByIdAsync(id);
            if (nearbyAttraction == null)
            {
                return NotFound();
            }

            _mapper.Map(requestDto, nearbyAttraction);
            await _nearbyAttractionService.UpdateAsync(nearbyAttraction);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _nearbyAttractionService.DeleteAsync(id);
            return NoContent();
        }
    }
}