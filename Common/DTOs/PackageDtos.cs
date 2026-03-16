using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class PackageRequestDto : IValidatableObject
    {
        [Required]
        public Guid ApartmentId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        [Required]
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Price cannot be negative.")]
        public decimal Price { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        public bool? IsActive { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Max bookings must be at least 1.")]
        public int? MaxBookings { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Add custom validation here if needed
            yield break;
        }
    }

    public class PackageResponseDto
    {
        public Guid PackageId { get; set; }
        public Guid ApartmentId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public bool? IsActive { get; set; }
        public int? MaxBookings { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class PackageItemRequestDto : IValidatableObject
    {
        [Required]
        public Guid PackageId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ItemName { get; set; } = null!;

        public string? ItemDescription { get; set; }

        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Quantity cannot be less than 0.")]
        public decimal? Quantity { get; set; }

        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Estimated value cannot be less than 0.")]
        public decimal? EstimatedValue { get; set; }

        public int? SortOrder { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Add custom validation here if needed
            yield break;
        }
    }

    public class PackageItemResponseDto
    {
        public Guid PackageItemId { get; set; }
        public Guid PackageId { get; set; }
        public string ItemName { get; set; } = null!;
        public string? ItemDescription { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? EstimatedValue { get; set; }
        public int? SortOrder { get; set; }
    }
}