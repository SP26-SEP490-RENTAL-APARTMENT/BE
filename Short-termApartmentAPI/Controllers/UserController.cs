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
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public UserController(IUserService userService, IMapper mapper)
        {
            _userService = userService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _userService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<UserDto>(result));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _userService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var itemDtos = _mapper.Map<IEnumerable<UserDto>>(items);
            return Ok(new { Items = itemDtos, TotalCount = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = _mapper.Map<User>(userDto);
            user.CreatedAt = DateTime.UtcNow;

            var created = await _userService.CreateAsync(user);
            return CreatedAtAction(nameof(GetById), new { id = created.UserId }, _mapper.Map<UserDto>(created));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            _mapper.Map(userDto, user);
            await _userService.UpdateAsync(user);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }
    }
}