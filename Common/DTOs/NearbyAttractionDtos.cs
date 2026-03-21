using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class CreateNearbyAttractionRequestDto : IValidatableObject
    {
        [Required]
        [MaxLength(255)]
        public string NameEn { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string NameVi { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Type { get; set; } = null!;

        [Required]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
        public double Latitude { get; set; }

        [Required]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
        public double Longitude { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }

    public class UpdateNearbyAttractionRequestDto : IValidatableObject
    {
        [MaxLength(255)]
        public string? NameEn { get; set; }

        [MaxLength(255)]
        public string? NameVi { get; set; }

        [MaxLength(100)]
        public string? Type { get; set; }

        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
        public double? Latitude { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if ((Latitude.HasValue && !Longitude.HasValue) || (!Latitude.HasValue && Longitude.HasValue))
            {
                yield return new ValidationResult("Both Latitude and Longitude must be provided if location is being updated.", new[] { nameof(Latitude), nameof(Longitude) });
            }
        }
    }

    public class NearbyAttractionResponseDto
    {
        public Guid AttractionId { get; set; }
        public string NameEn { get; set; } = null!;
        public string NameVi { get; set; } = null!;
        public string Type { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
    }
}
