using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace Common.DTOs;

public class CreateApartmentRequestDto
{
    [Required]
    [MaxLength(200)]
    public string title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string description { get; set; } = string.Empty;

    [Required]
    [Range(1, sbyte.MaxValue, ErrorMessage = "MaxOccupants must be at least 1.")]
    public sbyte? maxOccupants { get; set; }

    [Range(0, sbyte.MaxValue, ErrorMessage = "MaxInfants cannot be negative.")]
    public sbyte? maxInfants { get; set; }

    [Required]
    public bool? isPetAllowed { get; set; }

    [Range(0, sbyte.MaxValue, ErrorMessage = "MaxPets cannot be negative.")]
    public sbyte? maxPets { get; set; }

    [Required]
    [MaxLength(255)]
    public string? address { get; set; }

    [Required]
    [MaxLength(100)]
    public string? district { get; set; }

    [Required]
    [MaxLength(100)]
    public string? city { get; set; }

    [Required]
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? latitude { get; set; }
    
    [Required]
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? longitude { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "BasePricePerNight must be a positive value.")]
    public decimal basePricePerNight { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one photo is required to submit an apartment.")]
    public List<IFormFile> photos { get; set; } = new List<IFormFile>();
}

public class UpdateApartmentRequestDto
{
    [MaxLength(200)]
    public string? Title { get; set; }
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [Range(1, sbyte.MaxValue, ErrorMessage = "MaxOccupants must be at least 1.")]
    public sbyte? MaxOccupants { get; set; }

    [Range(0, sbyte.MaxValue, ErrorMessage = "MaxInfants cannot be negative.")]
    public sbyte? MaxInfants { get; set; }

    [Range(0, sbyte.MaxValue, ErrorMessage = "MaxPets cannot be negative.")]
    public sbyte? MaxPets { get; set; }
    
    public bool? IsPetAllowed { get; set; }
    
    [MaxLength(255)]
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? District { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }
    
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }
    
    [Range(0, double.MaxValue, ErrorMessage = "BasePricePerNight must be a positive value.")]
    public decimal? BasePricePerNight { get; set; }
}

public class CreateApartmentResponseDto
{
    public Guid ApartmentId { get; set; }

    public Guid LandlordId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public sbyte? MaxOccupants { get; set; }

    public sbyte? MaxInfants { get; set; }

    public sbyte? MaxPets { get; set; }

    public bool? IsPetAllowed { get; set; }

    public string? Address { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal BasePricePerNight { get; set; }

    public string? Status { get; set; }

    public string? BookingStatus { get; set; }

    public DateTime? CreatedAt { get; set; }

    public List<string> Photos { get; set; } = new List<string>();
}

public class ApartmentResponseDto
{
    public Guid ApartmentId { get; set; }
    public Guid LandlordId { get; set; }
    public string? LandlordName { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public sbyte? MaxOccupants { get; set; }
    public sbyte? MaxInfants { get; set; }
    public sbyte? MaxPets { get; set; }
    public bool? IsPetAllowed { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal BasePricePerNight { get; set; }
    public string? Status { get; set; }
    public string? BookingStatus { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsFavorite { get; set; }
    public Guid? CollectionId { get; set; }
    public string? InspectionStatus { get; set; }
    public List<ApartmentPriceChangeDto> PriceChanges { get; set; } = new List<ApartmentPriceChangeDto>();
    public List<string> Photos { get; set; } = new List<string>();
    public RoomResponseDto? Room { get; set; }
    public List<AmenityResponseDto> Amenities { get; set; } = new List<AmenityResponseDto>();
    public ApartmentNearbyAttractionsDto NearbyAttractions { get; set; } = new ApartmentNearbyAttractionsDto();
}

public class ApartmentNearbyAttractionDto
{
    public Guid AttractionId { get; set; }
    public string NameEn { get; set; } = null!;
    public string NameVi { get; set; } = null!;
    public string Type { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public double DistanceKm { get; set; }
}

public class ApartmentNearbyAttractionsDto
{
    public double PrimaryRadiusKm { get; set; } = 3;
    public double ExpandedRadiusKm { get; set; } = 5;
    public bool HasExpandedAttractions { get; set; }
    public List<ApartmentNearbyAttractionDto> PrimaryAttractions { get; set; } = new List<ApartmentNearbyAttractionDto>();
    public List<ApartmentNearbyAttractionDto> ExpandedAttractions { get; set; } = new List<ApartmentNearbyAttractionDto>();
}

public class ApartmentPriceChangeDto
{
    public decimal OldPricePerNight { get; set; }
    public decimal NewPricePerNight { get; set; }
    public string? Reason { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

/// <summary>
/// DTO for landlord to submit apartment for admin review.
/// Validates that apartment has required details (photos, pricing, amenities) before submission.
/// </summary>
public class SubmitForReviewDto
{
    [MaxLength(500)]
    public string? SubmissionNotes { get; set; }
}

/// <summary>
/// DTO for admin to approve or reject a submitted apartment.
/// </summary>
public class ApproveListingDto
{
    [Required]
    public bool Approved { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Response DTO after apartment listing is reviewed (approved or rejected).
/// </summary>
public class ListingReviewResponseDto
{
    public Guid ApartmentId { get; set; }
    public string Status { get; set; } = null!;
    public string? ReviewerNotes { get; set; }
    public DateTime ReviewedAt { get; set; }
    public string ReviewedBy { get; set; } = null!; // Admin/Staff username
}

/// <summary>
/// DTO representing an available date range with pricing information.
/// </summary>
public class DateRangePriceDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? PricePerNight { get; set; }
}

/// <summary>
/// DTO representing an unavailable (blocked) date range with optional blocking details.
/// Blocking details (BookingId, BookingStatus) are only included for landlord/staff roles.
/// </summary>
public class DateRangeBlockingDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public Guid? BookingId { get; set; }
    public string? BookingStatus { get; set; }
}

/// <summary>
/// DTO for apartment availability calendar response.
/// Shows available and unavailable date ranges for apartment bookings over a specified period.
/// All dates are in UTC.
/// </summary>
public class AvailabilityCalendarResponseDto
{
    public Guid ApartmentId { get; set; }
    public string? BookingStatus { get; set; }
    public DateTime CalendarStartDate { get; set; }
    public DateTime CalendarEndDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public IList<DateRangePriceDto> AvailablePeriods { get; set; } = new List<DateRangePriceDto>();
    public IList<DateRangeBlockingDto> UnavailablePeriods { get; set; } = new List<DateRangeBlockingDto>();
}

public class AvailabilityRangeItemDto
{
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }
}

public class SetApartmentAvailabilityRequestDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one range is required.")]
    public List<AvailabilityRangeItemDto> Ranges { get; set; } = new List<AvailabilityRangeItemDto>();
}

public class AvailabilityAppliedRangeDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
}

public class SetApartmentAvailabilityResponseDto
{
    public Guid ApartmentId { get; set; }
    public int SubmittedRanges { get; set; }
    public int AppliedRanges { get; set; }
    public IList<AvailabilityAppliedRangeDto> MergedRanges { get; set; } = new List<AvailabilityAppliedRangeDto>();
}

public class AvailabilityRemovalRangeDto
{
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public class RemoveApartmentAvailabilityRequestDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one range is required.")]
    public List<AvailabilityRemovalRangeDto> Ranges { get; set; } = new List<AvailabilityRemovalRangeDto>();
}

public class RemoveApartmentAvailabilityResponseDto
{
    public Guid ApartmentId { get; set; }
    public int SubmittedRanges { get; set; }
    public int AffectedRules { get; set; }
    public int RemainingRanges { get; set; }
    public IList<AvailabilityAppliedRangeDto> UpdatedRanges { get; set; } = new List<AvailabilityAppliedRangeDto>();
}

public class UnpublishApartmentDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}