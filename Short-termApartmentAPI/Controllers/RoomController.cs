using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IMapper _mapper;

    public RoomController(IRoomService roomService, IMapper mapper)
    {
        _roomService = roomService;
        _mapper = mapper;
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var room = await _roomService.GetByIdAsync(id);
        if (room == null)
            return NotFound(new ApiResponse<string>("Room not found."));

        return Ok(new ApiResponse<RoomResponseDto>(_mapper.Map<RoomResponseDto>(room)));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null)
    {
        var (items, total) = await _roomService.GetAllAsync(page, pageSize, sortBy, sortOrder, search);
        var dtos = _mapper.Map<IEnumerable<RoomResponseDto>>(items);
        return Ok(new { Items = dtos, TotalCount = total });
    }

    [Authorize(Roles = "landlord")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var room = _mapper.Map<Room>(dto);
        room.CreatedAt = DateTime.UtcNow;
        var created = await _roomService.CreateAsync(room);
        return CreatedAtAction(nameof(GetById), new { id = created.RoomId }, new ApiResponse<RoomResponseDto>(_mapper.Map<RoomResponseDto>(created)));
    }

    [Authorize(Roles = "landlord")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoomRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var room = await _roomService.GetByIdAsync(id);
        if (room == null)
            return NotFound(new ApiResponse<string>("Room not found."));

        _mapper.Map(dto, room);
        await _roomService.UpdateAsync(room);
        return NoContent();
    }

    [Authorize(Roles = "landlord")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _roomService.DeleteAsync(id);
        return NoContent();
    }
}