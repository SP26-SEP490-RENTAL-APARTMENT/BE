using DAL.Models;

namespace BLL.Services.Interfaces;

public interface ISmartPricingHistoryService : IBaseService<SmartPricingHistory>
{
    /// <summary>
    /// Generates a smart price suggestion for an apartment on a specific date based on occupancy rate.
    /// Algorithm: SuggestedPrice = BasePrice × (1 + (OccupancyRate × PriceAdjustmentFactor))
    /// Example: BasePrice=100, OccupancyRate=0.8, Factor=0.25 → SuggestedPrice = 100 × (1 + 0.2) = 120
    /// </summary>
    /// <param name="apartmentId">The apartment to price</param>
    /// <param name="date">The date for pricing</param>
    /// <param name="occupancyRate">Market occupancy rate (0-1). If null, defaults to 0.7</param>
    /// <returns>SmartPricingHistory record with suggested price</returns>
    Task<SmartPricingHistory> SuggestPriceAsync(Guid apartmentId, DateOnly date, decimal? occupancyRate = null);

    /// <summary>
    /// Landlord accepts or overrides the suggested price.
    /// </summary>
    Task<SmartPricingHistory> AcceptPriceSuggestionAsync(Guid pricingId, decimal? overridePrice = null);
}
