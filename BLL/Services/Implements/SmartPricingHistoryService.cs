using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;

namespace BLL.Services.Implements;

public sealed class SmartPricingHistoryService : BaseService<SmartPricingHistory>, ISmartPricingHistoryService
{
    private readonly IRepository<SmartPricingHistory> _repository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IHolidaysEventRepository _holidaysEventRepository;
    private readonly INearbyAttractionRepository _nearbyAttractionRepository;

    private const decimal DefaultOccupancyRate = 0.7m;
    private const decimal PriceAdjustmentFactor = 0.25m; // 25% increase per occupancy percentage

    public SmartPricingHistoryService(
        IRepository<SmartPricingHistory> repository,
        IApartmentRepository apartmentRepository,
        IHolidaysEventRepository holidaysEventRepository,
        INearbyAttractionRepository nearbyAttractionRepository)
        : base(repository)
    {
        _repository = repository;
        _apartmentRepository = apartmentRepository;
        _holidaysEventRepository = holidaysEventRepository;
        _nearbyAttractionRepository = nearbyAttractionRepository;
    }

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

        // Base occupancy component: BasePrice × (1 + (OccupancyRate × PriceAdjustmentFactor))
        var occupancyComponent = 1 + (rate * PriceAdjustmentFactor);

        // Holiday and location multipliers
        var holidayMultiplier = await GetHolidayMultiplierAsync(apartment, date);
        var locationMultiplier = await GetLocationMultiplierAsync(apartment);

        var finalMultiplier = occupancyComponent * holidayMultiplier * locationMultiplier;
        var suggestedPrice = apartment.BasePricePerNight * finalMultiplier;

        // Check if pricing suggestion already exists for this date
        var existing = await _repository.FindAsync(p => 
            p.ApartmentId == apartmentId && p.Date == date);

        SmartPricingHistory pricing;
        if (existing.Any())
        {
            // Update existing suggestion
            pricing = existing.First();
            pricing.BasePrice = apartment.BasePricePerNight;
            pricing.OccupancyRate = rate;
            pricing.Multiplier = (decimal)finalMultiplier;
            pricing.SuggestedPrice = suggestedPrice;
            pricing.Reason = $"Updated suggestion: {rate:P0} occupancy × {holidayMultiplier:F2} holiday × {locationMultiplier:F2} location";
            pricing.AcceptedByLandlord = false;

            _repository.Update(pricing);
            await _repository.SaveChangesAsync();
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
                Multiplier = (decimal)finalMultiplier,
                SuggestedPrice = suggestedPrice,
                Reason = $"Smart suggestion: {rate:P0} occupancy × {holidayMultiplier:F2} holiday × {locationMultiplier:F2} location → {suggestedPrice:C}",
                AcceptedByLandlord = false,
                CreatedAt = Common.Utils.VietnamTime.Now
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

    private async Task<decimal> GetHolidayMultiplierAsync(Apartment apartment, DateOnly date)
    {
        // Default: no holiday impact
        decimal multiplier = 1.0m;

        var locationScope = string.IsNullOrWhiteSpace(apartment.City)
            ? "Vietnam"
            : apartment.City!;

        var holidays = await _holidaysEventRepository.FindAsync(h =>
            h.StartDate <= date && h.EndDate >= date &&
            (h.LocationScope == null || h.LocationScope == "" || h.LocationScope == locationScope));

        if (!holidays.Any())
        {
            return multiplier;
        }

        // Choose the strongest applicable event type
        foreach (var holiday in holidays)
        {
            switch (holiday.EventType)
            {
                case "national_holiday":
                    multiplier = Math.Max(multiplier, 1.25m);
                    break;
                case "local_festival":
                case "international_event":
                case "sports_event":
                    multiplier = Math.Max(multiplier, 1.15m);
                    break;
                case "major_conference":
                    multiplier = Math.Max(multiplier, 1.10m);
                    break;
                case "school_break":
                    multiplier = Math.Max(multiplier, 1.05m);
                    break;
                default:
                    multiplier = Math.Max(multiplier, 1.05m);
                    break;
            }
        }

        return multiplier;
    }

    private async Task<decimal> GetLocationMultiplierAsync(Apartment apartment)
    {
        // Default: neutral location
        decimal multiplier = 1.0m;

        // If we don't have coordinates, fall back to city-based density (if available in future)
        if (apartment.Location == null)
        {
            return multiplier;
        }

        // Simple heuristic: count attractions in the same city
        var city = apartment.City;
        if (string.IsNullOrWhiteSpace(city))
        {
            return multiplier;
        }

        var attractions = await _nearbyAttractionRepository.FindAsync(a => a.City == city);
        var count = attractions.Count();

        if (count >= 20)
        {
            multiplier = 1.15m; // high density
        }
        else if (count >= 10)
        {
            multiplier = 1.08m; // medium density
        }
        else if (count >= 3)
        {
            multiplier = 1.03m; // low but noticeable
        }

        return multiplier;
    }
}
