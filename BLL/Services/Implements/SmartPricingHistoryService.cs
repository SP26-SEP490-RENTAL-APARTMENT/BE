using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class SmartPricingHistoryService(
    IRepository<SmartPricingHistory> repository,
    IApartmentRepository apartmentRepository)
    : BaseService<SmartPricingHistory>(repository), ISmartPricingHistoryService
{
    private readonly IApartmentRepository _apartmentRepository = apartmentRepository;
    private const decimal DefaultOccupancyRate = 0.7m;
    private const decimal PriceAdjustmentFactor = 0.25m; // 25% increase per occupancy percentage

    public async Task<SmartPricingHistory> SuggestPriceAsync(Guid apartmentId, DateOnly date, decimal? occupancyRate = null)
    {
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        // Use provided occupancy rate or default
        var rate = occupancyRate ?? DefaultOccupancyRate;

        // Validate occupancy rate is between 0 and 1
        if (rate < 0 || rate > 1)
            throw new ArgumentException("Occupancy rate must be between 0 and 1.");

        // Calculate suggested price: BasePrice × (1 + (OccupancyRate × PriceAdjustmentFactor))
        var multiplier = 1 + (rate * PriceAdjustmentFactor);
        var suggestedPrice = apartment.BasePricePerNight * multiplier;

        // Check if pricing suggestion already exists for this date
        var existing = await repository.FindAsync(p => 
            p.ApartmentId == apartmentId && p.Date == date);

        SmartPricingHistory pricing;
        if (existing.Any())
        {
            // Update existing suggestion
            pricing = existing.First();
            pricing.BasePrice = apartment.BasePricePerNight;
            pricing.OccupancyRate = rate;
            pricing.Multiplier = (decimal)multiplier;
            pricing.SuggestedPrice = suggestedPrice;
            pricing.Reason = $"Updated suggestion based on {rate:P0} occupancy rate";
            pricing.AcceptedByLandlord = false;

            repository.Update(pricing);
            await repository.SaveChangesAsync();
        }
        else
        {
            // Create new suggestion
            pricing = new SmartPricingHistory
            {
                PricingId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                Date = date,
                BasePrice = apartment.BasePricePerNight,
                OccupancyRate = rate,
                Multiplier = (decimal)multiplier,
                SuggestedPrice = suggestedPrice,
                Reason = $"Smart suggestion: {rate:P0} occupancy → {suggestedPrice:C}",
                AcceptedByLandlord = false,
                CreatedAt = DateTime.UtcNow
            };

            await CreateAsync(pricing);
        }

        return pricing;
    }

    public async Task<SmartPricingHistory> AcceptPriceSuggestionAsync(Guid pricingId, decimal? overridePrice = null)
    {
        var pricing = await GetByIdAsync(pricingId);
        if (pricing == null)
            throw new ArgumentException("Price suggestion not found.");

        if (overridePrice.HasValue)
        {
            if (overridePrice.Value <= 0)
                throw new ArgumentException("Override price must be positive.");

            pricing.SuggestedPrice = overridePrice.Value;
            pricing.Reason = $"Landlord override: {overridePrice:C}";
        }

        pricing.AcceptedByLandlord = true;
        
        await UpdateAsync(pricing);
        return pricing;
    }
}
