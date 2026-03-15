using System;

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

    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? RoomType { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(50)]
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

    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? RoomType { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? BedType { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, double.MaxValue, ErrorMessage = "Size must be a positive value.")]
    public decimal? SizeSqm { get; set; }

    public bool? IsPrivateBathroom { get; set; }
}