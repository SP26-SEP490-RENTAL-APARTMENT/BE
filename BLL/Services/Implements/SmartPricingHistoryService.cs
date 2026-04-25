using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace BLL.Services.Implements;

public sealed class SmartPricingHistoryService : BaseService<SmartPricingHistory>, ISmartPricingHistoryService
{
    private readonly IRepository<SmartPricingHistory> _repository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IApartmentPriceCalendarRepository _apartmentPriceCalendarRepository;
    private readonly IHolidaysEventRepository _holidaysEventRepository;
    private readonly INearbyAttractionRepository _nearbyAttractionRepository;

    private const decimal DefaultOccupancyRate = 0.7m;
    private const decimal PriceAdjustmentFactor = 0.25m; // 25% increase per occupancy percentage
    private const int ReasonMaxLength = 255;
    private static readonly string[] AllowedColumns =
    [
        nameof(SmartPricingHistory.PricingId),
        nameof(SmartPricingHistory.ApartmentId),
        nameof(SmartPricingHistory.Date),
        nameof(SmartPricingHistory.SuggestedPrice),
        nameof(SmartPricingHistory.BasePrice),
        nameof(SmartPricingHistory.Multiplier),
        nameof(SmartPricingHistory.Reason),
        nameof(SmartPricingHistory.OccupancyRate),
        nameof(SmartPricingHistory.AcceptedByLandlord),
        nameof(SmartPricingHistory.CreatedAt),
        nameof(Apartment.Title)
    ];

    public SmartPricingHistoryService(
        IRepository<SmartPricingHistory> repository,
        IApartmentRepository apartmentRepository,
        IApartmentPriceCalendarRepository apartmentPriceCalendarRepository,
        IHolidaysEventRepository holidaysEventRepository,
        INearbyAttractionRepository nearbyAttractionRepository)
        : base(repository)
    {
        _repository = repository;
        _apartmentRepository = apartmentRepository;
        _apartmentPriceCalendarRepository = apartmentPriceCalendarRepository;
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
            pricing.Reason = BuildPricingReason(
                isUpdated: true,
                occupancyRate: rate,
                occupancyComponent: occupancyComponent,
                holidayMultiplier: holidayMultiplier,
                locationMultiplier: locationMultiplier,
                finalMultiplier: finalMultiplier,
                basePrice: apartment.BasePricePerNight,
                suggestedPrice: suggestedPrice);
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
                Reason = BuildPricingReason(
                    isUpdated: false,
                    occupancyRate: rate,
                    occupancyComponent: occupancyComponent,
                    holidayMultiplier: holidayMultiplier,
                    locationMultiplier: locationMultiplier,
                    finalMultiplier: finalMultiplier,
                    basePrice: apartment.BasePricePerNight,
                    suggestedPrice: suggestedPrice),
                AcceptedByLandlord = false,
                CreatedAt = Common.Utils.VietnamTime.Now
            };

            await CreateAsync(pricing);
        }

        var apartmentDetails = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        pricing.Apartment = apartmentDetails ?? apartment;

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

        await UpsertManualOverrideCalendarAsync(pricing.ApartmentId, pricing.Date, pricing.SuggestedPrice);

        var apartmentDetails = await _apartmentRepository.GetApartmentWithDetailsAsync(pricing.ApartmentId);
        if (apartmentDetails != null)
        {
            pricing.Apartment = apartmentDetails;
        }

        return pricing;
    }

    public async Task<bool> HasAcceptedSuggestionAsync(Guid apartmentId)
    {
        var accepted = await _repository.FindAsync(p =>
            p.ApartmentId == apartmentId &&
            p.AcceptedByLandlord == true);

        return accepted.Any();
    }

    public async Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetAllSuggestionsAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;

        var (items, totalCount) = await GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, AllowedColumns);
        var hydratedItems = await HydrateApartmentsAsync(items);

        return (hydratedItems, totalCount);
    }

    public async Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetSuggestionsForLandlordAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;

        var apartments = await _apartmentRepository.FindAsync(a => a.LandlordId == landlordId);
        var apartmentIds = apartments.Select(a => a.ApartmentId).Distinct().ToHashSet();

        if (apartmentIds.Count == 0)
        {
            return (Enumerable.Empty<SmartPricingHistory>(), 0);
        }

        var suggestions = await _repository.FindAsync(s => apartmentIds.Contains(s.ApartmentId));
        var query = suggestions.AsQueryable();

        query = ApplyFilters(query, filters);
        query = ApplySearch(query, search);
        query = ApplySorting(query, sortBy, sortOrder);

        var totalCount = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var hydratedItems = await HydrateApartmentsAsync(items);
        return (hydratedItems, totalCount);
    }

    private async Task UpsertManualOverrideCalendarAsync(Guid apartmentId, DateOnly date, decimal acceptedPrice)
    {
        var existingOverrides = await _apartmentPriceCalendarRepository.FindAsync(c =>
            c.ApartmentId == apartmentId &&
            c.PriceType != null &&
            c.PriceType.ToLower() == "manual_override" &&
            c.StartDate <= date &&
            c.EndDate > date);

        var overrideEntry = existingOverrides
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (overrideEntry != null)
        {
            overrideEntry.DiscountPercentage = acceptedPrice;
            overrideEntry.IsDiscount = false;
            overrideEntry.UpdatedAt = Common.Utils.VietnamTime.Now;
            _apartmentPriceCalendarRepository.Update(overrideEntry);
            await _apartmentPriceCalendarRepository.SaveChangesAsync();
            return;
        }

        await _apartmentPriceCalendarRepository.AddAsync(new ApartmentPriceCalendar
        {
            PriceId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            StartDate = date,
            EndDate = date.AddDays(1),
            DiscountPercentage = acceptedPrice,
            IsDiscount = false,
            PriceType = "manual_override",
            MinNights = 1,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        });

        await _apartmentPriceCalendarRepository.SaveChangesAsync();
    }

    private static IQueryable<SmartPricingHistory> ApplyFilters(
        IQueryable<SmartPricingHistory> query,
        Dictionary<string, string>? filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return query;
        }

        foreach (var filter in filters)
        {
            var property = ResolveProperty(filter.Key);
            if (property == null || !TryConvertFilterValue(property.PropertyType, filter.Value, out var convertedValue))
            {
                continue;
            }

            query = query.Where(item => Equals(property.GetValue(item), convertedValue));
        }

        return query;
    }

    private static IQueryable<SmartPricingHistory> ApplySearch(
        IQueryable<SmartPricingHistory> query,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim();
        return query.Where(item =>
            (item.Reason != null && item.Reason.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) ||
            item.ApartmentId.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
    }

    private static IQueryable<SmartPricingHistory> ApplySorting(
        IQueryable<SmartPricingHistory> query,
        string? sortBy,
        string? sortOrder)
    {
        var property = ResolveProperty(sortBy) ?? ResolveProperty(nameof(SmartPricingHistory.CreatedAt));
        if (property == null)
        {
            return query;
        }

        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return descending
            ? query.OrderByDescending(item => property.GetValue(item))
            : query.OrderBy(item => property.GetValue(item));
    }

    private static PropertyInfo? ResolveProperty(string? column)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return typeof(SmartPricingHistory)
            .GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, column, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryConvertFilterValue(Type propertyType, string rawValue, out object? convertedValue)
    {
        convertedValue = null;

        var isNullable = Nullable.GetUnderlyingType(propertyType) != null;
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (string.Equals(rawValue, "null", StringComparison.OrdinalIgnoreCase))
        {
            if (isNullable || !propertyType.IsValueType)
            {
                convertedValue = null;
                return true;
            }

            return false;
        }

        object? parsedValue;

        if (targetType == typeof(Guid))
        {
            if (!Guid.TryParse(rawValue, out var guidValue))
            {
                return false;
            }

            parsedValue = guidValue;
        }
        else if (targetType == typeof(DateOnly))
        {
            if (!DateOnly.TryParse(rawValue, out var dateValue))
            {
                return false;
            }

            parsedValue = dateValue;
        }
        else if (targetType == typeof(DateTime))
        {
            if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var dateTimeValue)
                && !DateTime.TryParse(rawValue, out dateTimeValue))
            {
                return false;
            }

            parsedValue = dateTimeValue;
        }
        else if (targetType == typeof(bool))
        {
            if (bool.TryParse(rawValue, out var boolValue))
            {
                parsedValue = boolValue;
            }
            else if (rawValue == "1")
            {
                parsedValue = true;
            }
            else if (rawValue == "0")
            {
                parsedValue = false;
            }
            else
            {
                return false;
            }
        }
        else
        {
            try
            {
                parsedValue = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        if (isNullable)
        {
            convertedValue = Activator.CreateInstance(propertyType, parsedValue!);
            return true;
        }

        convertedValue = parsedValue;
        return true;
    }

    private static string BuildPricingReason(
        bool isUpdated,
        decimal occupancyRate,
        decimal occupancyComponent,
        decimal holidayMultiplier,
        decimal locationMultiplier,
        decimal finalMultiplier,
        decimal basePrice,
        decimal suggestedPrice)
    {
        var heading = isUpdated ? "Updated smart pricing" : "Smart pricing";
        var occupancyReason = $"occupancy {occupancyRate:P0}";
        var holidayReason = DescribeHolidayReason(holidayMultiplier);
        var locationReason = DescribeLocationReason(locationMultiplier);

        var occupancyFormula = $"occFactor=1+({occupancyRate:F2}x{PriceAdjustmentFactor:F2})={occupancyComponent:F4}";
        var multiplierFormula = $"finalMultiplier={occupancyComponent:F4}x{holidayMultiplier:F2}x{locationMultiplier:F2}={finalMultiplier:F4}";
        var priceFormula = $"price={basePrice:F2}x{finalMultiplier:F4}={suggestedPrice:F2}";

        var reason = $"{heading}: {occupancyReason}, holiday {holidayReason}, location {locationReason}. Calc: {occupancyFormula}; {multiplierFormula}; {priceFormula}.";

        return EnsureReasonMaxLength(reason);
    }

    private static string DescribeHolidayReason(decimal holidayMultiplier)
    {
        if (holidayMultiplier >= 1.25m)
        {
            return "national-holiday demand (x1.25)";
        }

        if (holidayMultiplier >= 1.15m)
        {
            return "festival/major-event demand (x1.15)";
        }

        if (holidayMultiplier >= 1.10m)
        {
            return "conference demand (x1.10)";
        }

        if (holidayMultiplier >= 1.05m)
        {
            return "minor seasonal demand (x1.05)";
        }

        return "no event impact (x1.00)";
    }

    private static string DescribeLocationReason(decimal locationMultiplier)
    {
        if (locationMultiplier >= 1.15m)
        {
            return "high attraction density (x1.15)";
        }

        if (locationMultiplier >= 1.08m)
        {
            return "medium attraction density (x1.08)";
        }

        if (locationMultiplier >= 1.03m)
        {
            return "low attraction density (x1.03)";
        }

        return "neutral local demand (x1.00)";
    }

    private static string EnsureReasonMaxLength(string reason)
    {
        if (reason.Length <= ReasonMaxLength)
        {
            return reason;
        }

        return reason.Substring(0, ReasonMaxLength - 3) + "...";
    }

    private async Task<List<SmartPricingHistory>> HydrateApartmentsAsync(IEnumerable<SmartPricingHistory> suggestions)
    {
        var list = suggestions.ToList();
        if (list.Count == 0)
        {
            return list;
        }

        var apartmentIds = list
            .Select(s => s.ApartmentId)
            .Distinct()
            .ToList();

        var apartments = new Dictionary<Guid, Apartment>();
        foreach (var apartmentId in apartmentIds)
        {
            var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
            if (apartment != null)
            {
                apartments[apartmentId] = apartment;
            }
        }

        foreach (var suggestion in list)
        {
            if (apartments.TryGetValue(suggestion.ApartmentId, out var apartment))
            {
                suggestion.Apartment = apartment;
            }
        }

        return list;
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
