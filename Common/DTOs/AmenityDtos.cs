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
    [StringLength(100, MinimumLength = 1, ErrorMessage = "English name must be between 1 and 100 characters.")]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Vietnamese name must be between 1 and 100 characters.")]
    public string NameVi { get; set; } = string.Empty;
}

public class UpdateAmenityRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "English name must be between 1 and 100 characters.")]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Vietnamese name must be between 1 and 100 characters.")]
    public string NameVi { get; set; } = string.Empty;
}
