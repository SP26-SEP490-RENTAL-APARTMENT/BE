using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

/// <summary>
/// Request to suggest a price for an apartment on a specific date.
/// </summary>
public class SuggestPriceDto
{
    [Required]
    public Guid ApartmentId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Range(0, 1, ErrorMessage = "Occupancy rate must be between 0 and 1.")]
    public decimal? OccupancyRate { get; set; }
}

/// <summary>
/// Response from price suggestion with calculated recommended price.
/// </summary>
public class SmartPricingResponseDto
{
    public Guid PricingId { get; set; }
    public Guid ApartmentId { get; set; }
    public DateOnly Date { get; set; }
    public decimal BasePrice { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal Multiplier { get; set; }
    public decimal SuggestedPrice { get; set; }
    public string? Reason { get; set; }
    public bool? AcceptedByLandlord { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Landlord accepts or overrides the suggested price.
/// </summary>
public class AcceptPriceSuggestionDto
{
    [Range(0, double.MaxValue, ErrorMessage = "Override price must be positive.")]
    public decimal? OverridePrice { get; set; }
}
