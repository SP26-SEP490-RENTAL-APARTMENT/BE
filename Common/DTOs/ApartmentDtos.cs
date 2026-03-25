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

    [Required]
    public bool? isPetAllowed { get; set; }

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

    public bool? IsPetAllowed { get; set; }

    public string? Address { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal BasePricePerNight { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public List<string> Photos { get; set; } = new List<string>();
}

public class ApartmentResponseDto
{
    public Guid ApartmentId { get; set; }
    public Guid LandlordId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public sbyte? MaxOccupants { get; set; }
    public bool? IsPetAllowed { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal BasePricePerNight { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<string> Photos { get; set; } = new List<string>();
    public RoomResponseDto? Room { get; set; }
    public List<AmenityResponseDto> Amenities { get; set; } = new List<AmenityResponseDto>();
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

    public required ICollection<string> ApprovalTags { get; set; }
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