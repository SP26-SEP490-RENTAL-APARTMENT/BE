using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class AmenityResponseDto
{
    public Guid AmenityId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameVi { get; set; } = string.Empty;
}

public class CreateAmenityRequestDto
{
    [Required]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    public string NameVi { get; set; } = string.Empty;
}

public class UpdateAmenityRequestDto
{
    [Required]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    public string NameVi { get; set; } = string.Empty;
}
