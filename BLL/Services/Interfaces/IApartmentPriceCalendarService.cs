using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IApartmentPriceCalendarService : IBaseService<ApartmentPriceCalendar>
{
    /// <summary>
    /// Upserts a single manual price range, handling conflict resolution 
    /// and merging adjacent ranges with the same policy.
    /// </summary>
    Task<PricingResultDto> UpsertManualRangeAsync(Guid apartmentId, ManualPriceRangeDto rangeDto, Guid landlordId);
    
    /// <summary>
    /// Bulk upserts prices for repeating days (e.g., every Tuesday).
    /// </summary>
    Task<PricingResultDto> BulkUpsertAsync(Guid apartmentId, BulkPriceUpdateDto updateDto, Guid landlordId);

    /// <summary>
    /// Deletes manual overrides for a specific date range.
    /// </summary>
    Task DeleteManualRangeAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate, Guid landlordId);
    
    /// <summary>
    /// Retrieves the resolved daily price for a month/period, 
    /// aggregating all rules (Base, Calendar, Manual).
    /// </summary>
    Task<IEnumerable<DailyPriceResolutionDto>> GetResolvedCalendarAsync(Guid apartmentId, DateOnly start, DateOnly end);
}
