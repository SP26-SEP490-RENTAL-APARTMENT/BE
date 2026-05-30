using System;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interfaces
{
    public interface IIdentityVerificationService
    {
        /// <summary>
        /// Ensures that the given user is identity-verified according to nationality-based rules
        /// before allowing booking-related actions. Throws InvalidOperationException if not verified.
        /// </summary>
        /// <param name="userId">The user identifier (tenant id for bookings).</param>
        Task EnsureUserVerifiedForBookingAsync(Guid userId);

        /// <summary>
        /// Ensures that a landlord is identity-verified before conducting property inspections.
        /// Throws InvalidOperationException if not verified.
        /// </summary>
        /// <param name="landlordId">The landlord user identifier.</param>
        Task EnsureUserVerifiedForInspectionAsync(Guid landlordId);

        /// <summary>
        /// Ensures that a landlord is identity-verified before submitting a property for review.
        /// Throws InvalidOperationException if not verified.
        /// </summary>
        /// <param name="landlordId">The landlord user identifier.</param>
        Task EnsureUserVerifiedForListingSubmissionAsync(Guid landlordId);

        /// <summary>
        /// Adds a new identity document for the specified user in pending status.
        /// </summary>
        /// <param name="userId">The owner of the document.</param>
        /// <param name="dto">Document upload data.</param>
        /// <returns>The created document identifiers.</returns>
        Task<Guid[]> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto);

        /// <summary>
        /// Marks a specific identity document as approved (verified) or rejected.
        /// </summary>
        /// <param name="dto">Review information.</param>
        Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto);

        /// <summary>
        /// Returns paged identity documents for the given user.
        /// </summary>
        Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetUserDocumentsAsync(
            Guid userId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null);

        /// <summary>
        /// Returns paged identity documents for all users. Staff/admin only at the controller level.
        /// </summary>
        Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetAllDocumentsAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null);
    }
}
