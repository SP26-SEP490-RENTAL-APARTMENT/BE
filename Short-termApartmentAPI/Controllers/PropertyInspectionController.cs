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
    [Authorize(Roles = "admin")]
    public class PropertyInspectionController : ControllerBase
    {
        private readonly IPropertyInspectionService _propertyInspectionService;
        private readonly IMapper _mapper;

        public PropertyInspectionController(IPropertyInspectionService propertyInspectionService, IMapper mapper)
        {
            _propertyInspectionService = propertyInspectionService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
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
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null)
        {
            var (items, totalCount) = await _propertyInspectionService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
            var mappedItems = _mapper.Map<IEnumerable<PropertyInspectionResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PropertyInspectionRequestDto propertyInspectionDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var propertyInspection = _mapper.Map<PropertyInspection>(propertyInspectionDto);
            var created = await _propertyInspectionService.CreateAsync(propertyInspection);
            return CreatedAtAction(nameof(GetById), new { id = created.InspectionId }, _mapper.Map<PropertyInspectionResponseDto>(created));
        }

        [HttpPut("{id:guid}")]
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

            _mapper.Map(propertyInspectionDto, propertyInspection);
            await _propertyInspectionService.UpdateAsync(propertyInspection);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _propertyInspectionService.DeleteAsync(id);
            return NoContent();
        }
    }
}
