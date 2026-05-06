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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateSupportTicketDto ticketDto)
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

            ticket.CreatedAt = Common.Utils.VietnamTime.Now; // Set created date or rely on DB
            ticket.UpdatedAt = Common.Utils.VietnamTime.Now;

            var created = await _supportTicketService.CreateTicketAsync(ticket);

            if (ticketDto.Files != null && ticketDto.Files.Any())
            {
                var uploadDto = new UploadSupportTicketAttachmentDto
                {
                    Files = ticketDto.Files,
                    Caption = null,
                    IsEvidence = true
                };

                await _supportTicketService.UploadTicketAttachmentsAsync(created.TicketId, uploadDto, userId);

                // optionally reload created ticket so response includes attachments:
                created = await _supportTicketService.GetByIdAsync(created.TicketId);
            }
            
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

        /// <summary>
        /// Resolves a support ticket by an authorized staff member.
        /// </summary>
        /// <param name="requestDto">The details including the ticket ID, resolution notes, and acting staff ID.</param>
        /// <returns>The updated ticket details.</returns>
        [HttpPost("{ticketId}/resolve")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> ResolveTicket(
            [FromRoute] Guid ticketId,
            [FromBody] string resolutionNotes)
        {
            // Basic validation check
            if (string.IsNullOrEmpty(resolutionNotes))
            {
                return BadRequest("Resolution details are required.");
            }

            var actorClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(actorClaim, out var actorUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                // Use the service layer logic
                var resolvedTicket = await _supportTicketService.ResolveTicketByStaffAsync(
                    ticketId,
                    resolutionNotes,
                    actorUserId);

                // Map the returned entity to a clean DTO for the client
                var dto = _mapper.Map<SupportTicketDto>(resolvedTicket);

                return Ok(dto);
            }
            catch (ArgumentException ex)
            {
                // Handles cases like "Support ticket not found" or "Resolution notes are required"
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Catch all other unexpected errors
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred: " + ex.Message);
            }
        }

        [HttpPatch("{ticketId}/status")]
        public async Task<IActionResult> UpdateTicketStatusByCreator(
        [FromRoute] Guid ticketId,
        [FromBody] UserUpdateStatusRequestDto requestDto)
        {
            if (requestDto == null || string.IsNullOrWhiteSpace(requestDto.NewStatus))
            {
                return BadRequest("Status change details (NewStatus) are required.");
            }

            var actorClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(actorClaim, out var actorUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                // The service handles all authorization checks (is creator, status validity, etc.)
                var updatedTicket = await _supportTicketService.UpdateTicketByCreatorStatusAsync(
                    ticketId,
                    actorUserId,
                    requestDto);

                // Map the returned entity to DTO
                var dto = _mapper.Map<SupportTicketDto>(updatedTicket);

                return Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Catch explicit authorization failures
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Catch business logic failures (e.g., already closed, invalid transition)
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                // Catch data validation failures
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Catch all unexpected errors
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred: " + ex.Message);
            }
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

        /// <summary>
        /// Upload evidence images/attachments to a support ticket.
        /// </summary>
        /// <param name="ticketId">The ticket ID to attach evidence to.</param>
        /// <param name="dto">The attachment upload request with files.</param>
        /// <returns>The uploaded attachments.</returns>
        [HttpPost("{ticketId:guid}/attachments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAttachments(
            [FromRoute] Guid ticketId,
            [FromForm] UploadSupportTicketAttachmentDto dto)
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
                var attachments = await _supportTicketService.UploadTicketAttachmentsAsync(ticketId, dto, userId);
                var attachmentDtos = _mapper.Map<IEnumerable<SupportTicketAttachmentDto>>(attachments);
                return Ok(new ApiResponse<IEnumerable<SupportTicketAttachmentDto>>(
                    attachmentDtos,
                    $"{attachments.Count()} file(s) uploaded successfully."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<string>($"An error occurred during upload: {ex.Message}"));
            }
        }
    }
}