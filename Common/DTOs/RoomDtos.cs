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