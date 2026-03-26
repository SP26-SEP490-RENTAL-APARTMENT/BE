using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/identity")]
    public class IdentityVerificationController : ControllerBase
    {
        private readonly IIdentityVerificationService _identityVerificationService;

        public IdentityVerificationController(IIdentityVerificationService identityVerificationService)
        {
            _identityVerificationService = identityVerificationService;
        }

        /// <summary>
        /// Upload a new identity document for the current user. The document will be marked as pending review.
        /// </summary>
        [HttpPost("documents")]
        [Authorize(Roles = "tenant,landlord,admin,staff")]
        public async Task<IActionResult> UploadDocument([FromBody] IdentityDocumentUploadDto dto)
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
                var documentId = await _identityVerificationService.AddIdentityDocumentAsync(userId, dto);
                return Ok(new ApiResponse<Guid>(documentId, "Identity document uploaded and pending verification."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        /// <summary>
        /// Mark an identity document as approved (verified) or rejected. Staff/admin only.
        /// </summary>
        [HttpPost("documents/review")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> ReviewDocument([FromBody] ReviewIdentityDocumentDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _identityVerificationService.ReviewIdentityDocumentAsync(dto);
                return Ok(new ApiResponse<string>("Identity document review updated."));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        /// <summary>
        /// List identity documents for the current user.
        /// </summary>
        [HttpGet("documents")]
        [Authorize(Roles = "tenant,landlord,admin,staff")]
        public async Task<IActionResult> GetMyDocuments()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var documents = await _identityVerificationService.GetUserDocumentsAsync(userId);
            return Ok(new ApiResponse<IdentityDocumentDto[]>(documents));
        }

        /// <summary>
        /// List identity documents for a specific user (staff/admin only).
        /// </summary>
        [HttpGet("users/{userId:guid}/documents")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> GetUserDocuments(Guid userId)
        {
            var documents = await _identityVerificationService.GetUserDocumentsAsync(userId);
            return Ok(new ApiResponse<IdentityDocumentDto[]>(documents));
        }
    }
}
