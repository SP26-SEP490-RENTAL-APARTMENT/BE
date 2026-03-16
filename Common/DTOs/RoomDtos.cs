using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class RoomResponseDto
{
    public Guid RoomId { get; set; }
    public Guid ApartmentId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? RoomType { get; set; }
    public string? BedType { get; set; }
    public decimal? SizeSqm { get; set; }
    public bool? IsPrivateBathroom { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateRoomRequestDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public Guid ApartmentId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Title { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Description { get; set; }
    [Required]
    [RegularExpression("^(private_single|private_double|shared_bed|studio|other)$", ErrorMessage = "Must be 'private_single','private_double','shared_bed','studio','other'")]
    public string? RoomType { get; set; }

    [Required]
    [RegularExpression("^(single|double|queen|king|bunk|shared)$", ErrorMessage = "Must be 'single','double','queen','king','bunk','shared'")]
    public string? BedType { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, double.MaxValue, ErrorMessage = "Size must be a positive value.")]
    public decimal? SizeSqm { get; set; }

    public bool? IsPrivateBathroom { get; set; }
}

public class UpdateRoomRequestDto
{
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Title { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(private_single|private_double|shared_bed|studio|other)$", ErrorMessage = "Must be 'private_single','private_double','shared_bed','studio','other'")]
    public string? RoomType { get; set; }

    [Required]
    [RegularExpression("^(single|double|queen|king|bunk|shared)$", ErrorMessage = "Must be 'single','double','queen','king','bunk','shared'")]
    public string? BedType { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, double.MaxValue, ErrorMessage = "Size must be a positive value.")]
    public decimal? SizeSqm { get; set; }

    public bool? IsPrivateBathroom { get; set; }
}