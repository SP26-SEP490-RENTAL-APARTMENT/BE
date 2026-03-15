using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class SubscriptionPlanDto
    {
        public Guid PlanId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal PriceMonthly { get; set; }
        public decimal? PriceAnnual { get; set; }
        public int? MaxApartments { get; set; }
        public int? MaxApartmentsPerApartment { get; set; }
        public string? Features { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateSubscriptionPlanDto
    {
        [Required]
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        [Required]
        public decimal PriceMonthly { get; set; }
        public decimal? PriceAnnual { get; set; }
        public int? MaxApartments { get; set; }
        public int? MaxApartmentsPerApartment { get; set; }
        public string? Features { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateSubscriptionPlanDto
    {
        [Required]
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        [Required]
        public decimal PriceMonthly { get; set; }
        public decimal? PriceAnnual { get; set; }
        public int? MaxApartments { get; set; }
        public int? MaxApartmentsPerApartment { get; set; }
        public string? Features { get; set; }
        public bool? IsActive { get; set; }
    }
}
