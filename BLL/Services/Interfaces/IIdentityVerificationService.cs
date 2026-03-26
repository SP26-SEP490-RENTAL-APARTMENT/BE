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
        /// Adds a new identity document for the specified user in pending status.
        /// </summary>
        /// <param name="userId">The owner of the document.</param>
        /// <param name="dto">Document metadata.</param>
        /// <returns>The created document identifier.</returns>
        Task<Guid> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto);

        /// <summary>
        /// Marks a specific identity document as approved (verified) or rejected.
        /// </summary>
        /// <param name="dto">Review information.</param>
        Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto);

        /// <summary>
        /// Returns all identity documents for the given user.
        /// </summary>
        Task<IdentityDocumentDto[]> GetUserDocumentsAsync(Guid userId);
    }
}
