using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace Common.DTOs;

public class CreateApartmentRequestDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public sbyte? MaxOccupants { get; set; }

    [Required]
    public bool? IsPetAllowed { get; set; }

    [Required]
    public string? Address { get; set; }

    [Required]
    public string? District { get; set; }

    [Required]
    public string? City { get; set; }

    [Required]
    public decimal? Latitude { get; set; }
    
    [Required]
    public decimal? Longitude { get; set; }

    [Required]
    public decimal BasePricePerNight { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one photo is required to submit an apartment.")]
    public List<IFormFile> Photos { get; set; } = new List<IFormFile>();
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