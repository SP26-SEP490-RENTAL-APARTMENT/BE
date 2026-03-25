using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IApartmentService : IBaseService<Apartment>
{
    Task<CreateApartmentResponseDto> CreateApartmentWithPhotosAsync(CreateApartmentRequestDto requestDto, Guid landlordId);
    Task<ApartmentResponseDto?> GetApartmentWithDetailsAsync(Guid id);
    Task AddAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds);
    Task UpdateApartmentPhotosAsync(Guid apartmentId, List<Microsoft.AspNetCore.Http.IFormFile> photos);

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
    /// Validates that apartment has required details for submission:
    /// - At least one photo
    /// - At least one amenity
    /// - Base price set
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    Task<bool> ValidateListingDetailsAsync(Guid apartmentId);
}
