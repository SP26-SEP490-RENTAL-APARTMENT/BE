using AutoMapper;
using System.Collections.Generic;
using System.Linq;
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
    public class PackageController : ControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IMapper _mapper;

        public PackageController(IPackageService packageService, IMapper mapper)
        {
            _packageService = packageService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
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

        [HttpPost("{id:guid}/items")]
        public async Task<IActionResult> AddItems(Guid id, [FromBody] List<Guid> packageItemIds)
        {
            if (packageItemIds == null || !packageItemIds.Any())
                return BadRequest("No package item ids provided.");

            try
            {
                await _packageService.AddItemsAsync(id, packageItemIds);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id:guid}/items/{itemId:guid}")]
        public async Task<IActionResult> RemoveItem(Guid id, Guid itemId)
        {
            try
            {
                await _packageService.RemoveItemAsync(id, itemId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
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
        public async Task<IActionResult> Delete(Guid id)
        {
            await _packageService.DeleteAsync(id);
            return NoContent();
        }
    }
}