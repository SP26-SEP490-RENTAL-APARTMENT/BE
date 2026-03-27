using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Available for all authenticated roles
    public class SupportTicketController : ControllerBase
    {
        private readonly ISupportTicketService _supportTicketService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public SupportTicketController(ISupportTicketService supportTicketService, IUserService userService, IMapper mapper)
        {
            _supportTicketService = supportTicketService;
            _userService = userService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _supportTicketService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isStaffOrAdmin = User.IsInRole("staff") || User.IsInRole("admin");

            if (!isStaffOrAdmin)
            {
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Forbid();
                }

                if (result.UserId != userId)
                {
                    return Forbid();
                }
            }

            return Ok(_mapper.Map<SupportTicketDto>(result));
        }

        [HttpGet]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _supportTicketService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var itemDtos = _mapper.Map<IEnumerable<SupportTicketDto>>(items);
            return Ok(new { Items = itemDtos, TotalCount = totalCount });
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyTickets(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            filters ??= new Dictionary<string, string>();
            filters["UserId"] = userId.ToString();

            var (items, totalCount) = await _supportTicketService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var itemDtos = _mapper.Map<IEnumerable<SupportTicketDto>>(items);
            return Ok(new { Items = itemDtos, TotalCount = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSupportTicketDto ticketDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ticket = _mapper.Map<SupportTicket>(ticketDto);
            ticket.UserId = userId; // Ensure the ticket is associated with the authenticated user

            ticket.CreatedAt = DateTime.UtcNow; // Set created date or rely on DB
            ticket.UpdatedAt = DateTime.UtcNow;

            var created = await _supportTicketService.CreateTicketAsync(ticket);
            return CreatedAtAction(nameof(GetById), new { id = created.TicketId }, _mapper.Map<SupportTicketDto>(created));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupportTicketDto ticketDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var actorClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(actorClaim, out var actorUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (ticketDto.ResolvedBy.HasValue)
            {
                var resolvedByUser = await _userService.GetByIdAsync(ticketDto.ResolvedBy.Value);
                if (resolvedByUser == null || !resolvedByUser.Role.Equals("staff", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new ApiResponse<string>("Only accept by StaffId"));
                }
            }

            var existing = await _supportTicketService.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            await _supportTicketService.UpdateTicketByStaffAsync(id, ticketDto, actorUserId);
            return NoContent();
        }

        [HttpPost("{id:guid}/report-persisting")]
        public async Task<IActionResult> ReportPersisting(Guid id, [FromBody] ReportPersistingIssueDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var followUp = await _supportTicketService.CreateFollowUpTicketAsync(id, userId, dto.Details);
                var response = _mapper.Map<SupportTicketDto>(followUp);
                return CreatedAtAction(nameof(GetById), new { id = followUp.TicketId },
                    new ApiResponse<SupportTicketDto>(response, "Follow-up ticket created and routed to staff."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _supportTicketService.DeleteAsync(id);
            return NoContent();
        }
    }
}