using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IApartmentService : IBaseService<Apartment>
{
    Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null);

    Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            IEnumerable<string>? allowedColumns = null,
            Dictionary<string, string>? filters = null);

    Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(
            int page,
            int pageSize,
            Guid landlordId,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            IEnumerable<string>? allowedColumns = null,
            Dictionary<string, string>? filters = null);

    Task<(IEnumerable<ApartmentResponseDto> Items, int TotalCount)> GetAllPublicResponseAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        Guid? tenantId = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null);


    Task<CreateApartmentResponseDto> CreateApartmentWithPhotosAsync(CreateApartmentRequestDto requestDto, Guid landlordId);
    Task<ApartmentResponseDto?> GetApartmentWithDetailsAsync(Guid id);
    Task<ApartmentResponseDto?> GetApartmentWithDetailsResponseAsync(Guid id, Guid? tenantId = null, bool includeExpandedNearbyAttractions = false);
    Task AddAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds);
    Task RemoveAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds);
    Task UpdateApartmentPhotosAsync(Guid apartmentId, List<Microsoft.AspNetCore.Http.IFormFile> photos);
    Task AddApartmentAttachmentsAsync(Guid apartmentId, List<Microsoft.AspNetCore.Http.IFormFile> files);
    Task RemoveApartmentAttachmentAsync(Guid apartmentId, Guid mediaId);

    /// <summary>
    /// Landlord submits apartment for admin review. Validates draft status and required details.
    /// Transitions apartment from 'draft' to 'pending_review'.
    /// </summary>
    Task<Apartment> SubmitForReviewAsync(Guid apartmentId, Guid landlordId, SubmitForReviewDto dto);

    /// <summary>
    /// Admin approves or rejects a pending_review apartment.
    /// Transitions from 'pending_review' to 'posted' (if approved) or 'blocked' (if rejected).
    /// </summary>
    Task<Apartment> ApproveListingAsync(Guid apartmentId, Guid adminId, ApproveListingDto dto);

    /// <summary>
    /// Unpublishes a posted apartment, transitioning it back to draft status.
    /// Can be called by landlord (owning the apartment) or admin/staff.
    /// </summary>
    Task<Apartment> UnpublishApartmentAsync(Guid apartmentId, Guid requesterId, string? reason = null);

    /// <summary>
    /// Validates that apartment has required details for submission:
    /// - At least one photo
    /// - At least one amenity
    /// - Base price set
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    Task<bool> ValidateListingDetailsAsync(Guid apartmentId);
}
