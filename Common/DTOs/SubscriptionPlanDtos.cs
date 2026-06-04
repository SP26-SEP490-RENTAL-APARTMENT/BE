using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class SubscriptionPlanDto
    {
        public Guid PlanId { get; set; }
        public string Name { get; set; } = null!;
        public string? NameVi { get; set; }
        public string? Description { get; set; }
        public string? DescriptionVi { get; set; }
        public decimal PriceMonthly { get; set; }
        public decimal? PriceAnnual { get; set; }
        public int? MaxApartments { get; set; }
        public string? Features { get; set; }
        public string? FeaturesVi { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateSubscriptionPlanDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        [MaxLength(100)]
        public string? NameVi { get; set; }
        public string? Description { get; set; }
        public string? DescriptionVi { get; set; }
        [Required]
        [Range(10000, double.MaxValue, ErrorMessage = "PriceMonthly must be > 10000")]
        public decimal PriceMonthly { get; set; }
        [Range(10000, double.MaxValue, ErrorMessage = "PriceAnnual must be > 10000")]
        public decimal? PriceAnnual { get; set; }
        [Range(1, 100, ErrorMessage = "MaxApartments must be at least 1-100.")]
        public int? MaxApartments { get; set; }
        public string? Features { get; set; }
        public string? FeaturesVi { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateSubscriptionPlanDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        [MaxLength(100)]
        public string? NameVi { get; set; }
        public string? Description { get; set; }
        public string? DescriptionVi { get; set; }
        [Required]
        [Range(10000, double.MaxValue, ErrorMessage = "PriceMonthly must be > 10000")]
        public decimal PriceMonthly { get; set; }
        [Range(10000, double.MaxValue, ErrorMessage = "PriceAnnual must be > 10000")]
        public decimal? PriceAnnual { get; set; }
        [Range(1, 100, ErrorMessage = "MaxApartments must be at least 1-100.")]
        public int? MaxApartments { get; set; }
        public string? Features { get; set; }
        public string? FeaturesVi { get; set; }
        public bool? IsActive { get; set; }
    }
}
