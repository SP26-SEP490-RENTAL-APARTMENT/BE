using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/identity")]
    public class IdentityVerificationController : ControllerBase
    {
        private const int DefaultPage = 1;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        private readonly IIdentityVerificationService _identityVerificationService;

        public IdentityVerificationController(IIdentityVerificationService identityVerificationService)
        {
            _identityVerificationService = identityVerificationService;
        }

        /// <summary>
        /// Upload a new identity document for the current user. The document will be marked as pending review.
        /// </summary>
        [HttpPost("documents")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "tenant,landlord,admin,staff")]
        public async Task<IActionResult> UploadDocument([FromForm] IdentityDocumentUploadDto dto)
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
                var documentIds = await _identityVerificationService.AddIdentityDocumentAsync(userId, dto);
                return Ok(new ApiResponse<Guid[]>(documentIds, "Identity document uploaded and pending verification."));
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
        /// List identity documents for all users (staff/admin only).
        /// </summary>
        [HttpGet("documents")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> GetAllDocuments(
            [FromQuery] int page = DefaultPage,
            [FromQuery] int pageSize = DefaultPageSize,
            [FromQuery] string? sortBy = "UploadedAt",
            [FromQuery] string? sortOrder = "desc")
        {
            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);
            var normalizedSortOrder = NormalizeSortOrder(sortOrder);

            var (items, totalCount) = await _identityVerificationService.GetAllDocumentsAsync(
                normalizedPage,
                normalizedPageSize,
                sortBy,
                normalizedSortOrder);

            return Ok(new ApiResponse<object>(new
            {
                Items = items,
                TotalCount = totalCount,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                SortBy = sortBy,
                SortOrder = normalizedSortOrder
            }));
        }

        /// <summary>
        /// List identity documents for the current user.
        /// </summary>
        [HttpGet("my-documents")]
        [Authorize(Roles = "tenant,landlord,admin,staff")]
        public async Task<IActionResult> GetMyDocuments(
            [FromQuery] int page = DefaultPage,
            [FromQuery] int pageSize = DefaultPageSize,
            [FromQuery] string? sortBy = "UploadedAt",
            [FromQuery] string? sortOrder = "desc")
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);
            var normalizedSortOrder = NormalizeSortOrder(sortOrder);

            var (items, totalCount) = await _identityVerificationService.GetUserDocumentsAsync(
                userId,
                normalizedPage,
                normalizedPageSize,
                sortBy,
                normalizedSortOrder);

            return Ok(new ApiResponse<object>(new
            {
                Items = items,
                TotalCount = totalCount,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                SortBy = sortBy,
                SortOrder = normalizedSortOrder
            }));
        }

        /// <summary>
        /// List identity documents for a specific user (staff/admin only).
        /// </summary>
        [HttpGet("users/{userId:guid}/documents")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> GetUserDocuments(
            Guid userId,
            [FromQuery] int page = DefaultPage,
            [FromQuery] int pageSize = DefaultPageSize,
            [FromQuery] string? sortBy = "UploadedAt",
            [FromQuery] string? sortOrder = "desc")
        {
            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);
            var normalizedSortOrder = NormalizeSortOrder(sortOrder);

            var (items, totalCount) = await _identityVerificationService.GetUserDocumentsAsync(
                userId,
                normalizedPage,
                normalizedPageSize,
                sortBy,
                normalizedSortOrder);

            return Ok(new ApiResponse<object>(new
            {
                Items = items,
                TotalCount = totalCount,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                SortBy = sortBy,
                SortOrder = normalizedSortOrder
            }));
        }

        private static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
        {
            var normalizedPage = page < 1 ? DefaultPage : page;
            var normalizedPageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
            return (normalizedPage, normalizedPageSize);
        }

        private static string NormalizeSortOrder(string? sortOrder)
        {
            return string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        }
    }
}
