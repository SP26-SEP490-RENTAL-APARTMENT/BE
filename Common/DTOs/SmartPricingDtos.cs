using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

/// <summary>
/// Request to suggest a price for an apartment over a date span.
/// </summary>
public class SuggestPriceDto
{
    [Required]
    public Guid ApartmentId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

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
    public SmartPricingApartmentPhotoDto? Apartment { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal BasePrice { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal Multiplier { get; set; }
    public decimal SuggestedPrice { get; set; }
    public string? Reason { get; set; }
    public bool? AcceptedByLandlord { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Apartment payload used in smart pricing response.
/// Contains photo URL information only.
/// </summary>
public class SmartPricingApartmentPhotoDto
{
    public IEnumerable<string> PhotoUrls { get; set; } = Enumerable.Empty<string>();
}

/// <summary>
/// Landlord accepts or overrides the suggested price.
/// </summary>
public class AcceptPriceSuggestionDto
{
    [Range(0, double.MaxValue, ErrorMessage = "Override price must be positive.")]
    public decimal? OverridePrice { get; set; }
}

/// <summary>
/// Paginated smart pricing suggestion response.
/// </summary>
public class SmartPricingListResponseDto
{
    public IEnumerable<SmartPricingResponseDto> Items { get; set; } = Enumerable.Empty<SmartPricingResponseDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
