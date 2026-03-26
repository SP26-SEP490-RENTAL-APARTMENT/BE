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
    public class PackageItemController : ControllerBase
    {
        private readonly IPackageItemService _packageItemService;
        private readonly IMapper _mapper;

        public PackageItemController(IPackageItemService packageItemService, IMapper mapper)
        {
            _packageItemService = packageItemService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _packageItemService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<PackageItemResponseDto>(result));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _packageItemService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var mappedItems = _mapper.Map<IEnumerable<PackageItemResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PackageItemRequestDto packageItemDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var packageItem = _mapper.Map<PackageItem>(packageItemDto);
            var created = await _packageItemService.CreateAsync(packageItem);
            return CreatedAtAction(nameof(GetById), new { id = created.PackageItemId }, _mapper.Map<PackageItemResponseDto>(created));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PackageItemRequestDto packageItemDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var packageItem = await _packageItemService.GetByIdAsync(id);
            if (packageItem == null)
            {
                return NotFound();
            }

            _mapper.Map(packageItemDto, packageItem);
            await _packageItemService.UpdateAsync(packageItem);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _packageItemService.DeleteAsync(id);
            return NoContent();
        }
    }
}
