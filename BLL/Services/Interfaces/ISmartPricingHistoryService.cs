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
    /// Generates a smart price suggestion for an apartment over a date span.
    /// The service computes the pricing per day and stores one suggestion record for the whole span.
    /// </summary>
    /// <param name="apartmentId">The apartment to price</param>
    /// <param name="startDate">Start of the pricing span</param>
    /// <param name="endDate">End of the pricing span</param>
    /// <param name="occupancyRate">Market occupancy rate (0-1). If null, defaults to 0.7</param>
    /// <returns>SmartPricingHistory record with suggested price</returns>
    Task<SmartPricingHistory> SuggestPriceAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate, decimal? occupancyRate = null);

    /// <summary>
    /// Landlord accepts or overrides the suggested price.
    /// </summary>
    Task<SmartPricingHistory> AcceptPriceSuggestionAsync(Guid pricingId, decimal? overridePrice = null);

    /// <summary>
    /// Checks whether the apartment has at least one accepted smart pricing recommendation.
    /// Used to show non-blocking submission warnings.
    /// </summary>
    Task<bool> HasAcceptedSuggestionAsync(Guid apartmentId);

    /// <summary>
    /// Gets all smart pricing suggestions for admin view.
    /// </summary>
    Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetAllSuggestionsAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Gets smart pricing suggestions for apartments owned by a specific landlord.
    /// </summary>
    Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetSuggestionsForLandlordAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null);
}
