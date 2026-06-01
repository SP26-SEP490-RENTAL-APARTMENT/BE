using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BLL.Services.Implements;
using DAL.Models;
using DAL.Repository.Interfaces;
using NetTopologySuite.Geometries;

namespace BLL.Tests;

public class SmartPricingHistoryServiceTests
{
    private static Apartment CreateDefaultApartment(Guid apartmentId, decimal basePrice, string city)
    {
        return new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Test Apartment",
            City = city,
            BasePricePerNight = basePrice,
            Location = new Point(0, 0)
        };
    }

    [Fact]
    public async Task SuggestPriceAsync_NoHoliday_NoAttractions_UsesOccupancyOnly()
    {
        // Arrange
        var apartmentId = Guid.NewGuid();
        var basePrice = 100m;
        var city = "Hanoi";
        var date = new DateOnly(2026, 3, 27);
        var occupancyRate = 0.7m;

        var apartment = CreateDefaultApartment(apartmentId, basePrice, city);

        var pricingRepo = new InMemorySmartPricingHistoryRepository();
        var apartmentRepo = new InMemoryApartmentRepository(apartment);
        var priceCalendarRepo = new InMemorySmartPricingCalendarRepository();
        var holidaysRepo = new InMemoryHolidaysEventRepository();
        var attractionsRepo = new InMemoryNearbyAttractionRepository();

        var service = new SmartPricingHistoryService(pricingRepo, apartmentRepo, priceCalendarRepo, holidaysRepo, attractionsRepo);

        // Act
        var pricing = await service.SuggestPriceAsync(apartmentId, date, occupancyRate);

        // Assert
        var expectedMultiplier = Math.Clamp(1m + ((occupancyRate - 0.55m) * 0.8m), 0.85m, 1.35m); // no holiday/location impact
        var expectedPrice = basePrice * expectedMultiplier;

        Assert.Equal(expectedMultiplier, pricing.Multiplier);
        Assert.Equal(expectedPrice, pricing.SuggestedPrice);
    }

    [Fact]
    public async Task SuggestPriceAsync_WithNationalHoliday_AppliesHolidayMultiplier()
    {
        // Arrange
        var apartmentId = Guid.NewGuid();
        var basePrice = 100m;
        var city = "Hanoi";
        var date = new DateOnly(2026, 4, 30); // sample holiday date
        var occupancyRate = 0.7m;

        var apartment = CreateDefaultApartment(apartmentId, basePrice, city);

        var pricingRepo = new InMemorySmartPricingHistoryRepository();
        var apartmentRepo = new InMemoryApartmentRepository(apartment);
        var priceCalendarRepo = new InMemorySmartPricingCalendarRepository();

        var holidaysRepo = new InMemoryHolidaysEventRepository(new List<HolidaysEvent>
        {
            new HolidaysEvent
            {
                EventId = Guid.NewGuid(),
                EventName = "National Holiday",
                EventType = "national_holiday",
                StartDate = date,
                EndDate = date,
                LocationScope = city
            }
        });

        var attractionsRepo = new InMemoryNearbyAttractionRepository();

        var service = new SmartPricingHistoryService(pricingRepo, apartmentRepo, priceCalendarRepo, holidaysRepo, attractionsRepo);

        // Act
        var pricing = await service.SuggestPriceAsync(apartmentId, date, occupancyRate);

        // Assert
        var occupancyComponent = Math.Clamp(1m + ((occupancyRate - 0.55m) * 0.8m), 0.85m, 1.35m);
        var expectedMultiplier = occupancyComponent * 1.25m; // national holiday multiplier
        var expectedPrice = basePrice * expectedMultiplier;

        Assert.Equal(expectedMultiplier, pricing.Multiplier);
        Assert.Equal(expectedPrice, pricing.SuggestedPrice);
        Assert.Contains("holiday", pricing.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestPriceAsync_WithHighAttractionDensity_AppliesLocationMultiplier()
    {
        // Arrange
        var apartmentId = Guid.NewGuid();
        var basePrice = 100m;
        var city = "Hanoi";
        var date = new DateOnly(2026, 5, 10);
        var occupancyRate = 0.7m;

        var apartment = CreateDefaultApartment(apartmentId, basePrice, city);

        var pricingRepo = new InMemorySmartPricingHistoryRepository();
        var apartmentRepo = new InMemoryApartmentRepository(apartment);
        var priceCalendarRepo = new InMemorySmartPricingCalendarRepository();
        var holidaysRepo = new InMemoryHolidaysEventRepository();

        var attractions = Enumerable.Range(0, 20)
            .Select(i => new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = $"Attraction {i}",
                NameVi = $"Điểm tham quan {i}",
                Type = "generic",
                City = city,
                Location = new Point(0, 0)
            })
            .ToList();

        var attractionsRepo = new InMemoryNearbyAttractionRepository(attractions);

        var service = new SmartPricingHistoryService(pricingRepo, apartmentRepo, priceCalendarRepo, holidaysRepo, attractionsRepo);

        // Act
        var pricing = await service.SuggestPriceAsync(apartmentId, date, occupancyRate);

        // Assert
        var occupancyComponent = Math.Clamp(1m + ((occupancyRate - 0.55m) * 0.8m), 0.85m, 1.35m);
        var expectedMultiplier = occupancyComponent * 1.15m; // high-density location multiplier
        var expectedPrice = basePrice * expectedMultiplier;

        Assert.Equal(expectedMultiplier, pricing.Multiplier);
        Assert.Equal(expectedPrice, pricing.SuggestedPrice);
        Assert.Contains("location", pricing.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptPriceSuggestionAsync_StoresAcceptedPriceAsFixedPriceForExactRange()
    {
        var apartmentId = Guid.NewGuid();
        var pricingId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 5, 28);
        var endDate = new DateOnly(2026, 5, 29);

        var apartment = CreateDefaultApartment(apartmentId, 100m, "Hanoi");
        var pricing = new SmartPricingHistory
        {
            PricingId = pricingId,
            ApartmentId = apartmentId,
            Date = startDate,
            StartDate = startDate,
            EndDate = endDate,
            SuggestedPrice = 145m,
            BasePrice = 100m,
            Reason = "test",
            CreatedAt = DateTime.UtcNow
        };

        var pricingRepo = new InMemorySmartPricingHistoryRepository(pricing);
        var apartmentRepo = new InMemoryApartmentRepository(apartment);
        var priceCalendarRepo = new InMemorySmartPricingCalendarRepository();
        var holidaysRepo = new InMemoryHolidaysEventRepository();
        var attractionsRepo = new InMemoryNearbyAttractionRepository();

        var service = new SmartPricingHistoryService(pricingRepo, apartmentRepo, priceCalendarRepo, holidaysRepo, attractionsRepo);

        await service.AcceptPriceSuggestionAsync(pricingId);

        var saved = priceCalendarRepo.Items.Single();
        Assert.Equal(startDate, saved.StartDate);
        Assert.Equal(endDate, saved.EndDate);
        Assert.Equal(145m, saved.FixedPricePerNight);
        Assert.Null(saved.DiscountPercentage);
        Assert.False(saved.IsDiscount == true);
        Assert.Equal("manual_override", saved.PriceType);
    }
}

internal sealed class InMemorySmartPricingHistoryRepository : IRepository<SmartPricingHistory>
{
    private readonly List<SmartPricingHistory> _items = new();

    public InMemorySmartPricingHistoryRepository(params SmartPricingHistory[] items)
    {
        _items.AddRange(items);
    }

    public Task AddAsync(SmartPricingHistory entity)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SmartPricingHistory>> FindAsync(Expression<Func<SmartPricingHistory, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<SmartPricingHistory> result = _items.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<SmartPricingHistory>> FindNoTrackingAsync(Expression<Func<SmartPricingHistory, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<SmartPricingHistory> result = _items.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((_items.AsEnumerable(), _items.Count));
    }

    public Task<SmartPricingHistory?> GetByIdAsync(Guid id)
    {
        SmartPricingHistory? result = _items.FirstOrDefault(p => p.PricingId == id);
        return Task.FromResult(result);
    }

    public void Remove(SmartPricingHistory entity)
    {
        _items.Remove(entity);
    }

    public void Update(SmartPricingHistory entity)
    {
        // No-op: entity is already updated by reference in the list
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

internal sealed class InMemorySmartPricingCalendarRepository : IApartmentPriceCalendarRepository
{
    private readonly List<ApartmentPriceCalendar> _items = new();

    public IReadOnlyList<ApartmentPriceCalendar> Items => _items;

    public void AddRange(IEnumerable<ApartmentPriceCalendar> newRecords) => _items.AddRange(newRecords);

    public void DeleteRange(IEnumerable<ApartmentPriceCalendar> recordsToDelete)
    {
        foreach (var record in recordsToDelete)
        {
            _items.RemoveAll(item => item.PriceId == record.PriceId);
        }
    }

    public Task AddAsync(ApartmentPriceCalendar entity)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ApartmentPriceCalendar>> FindAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(_items.Where(compiled));
    }

    public Task<IEnumerable<ApartmentPriceCalendar>> FindNoTrackingAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(_items.Where(compiled));
    }

    public Task<(IEnumerable<ApartmentPriceCalendar> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((_items.AsEnumerable(), _items.Count));
    }

    public Task<ApartmentPriceCalendar?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_items.FirstOrDefault(item => item.PriceId == id));
    }

    public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingManualRecords(Guid apartmentId, DateOnly start, DateOnly end)
    {
        var result = _items.Where(item => item.ApartmentId == apartmentId && item.StartDate <= end && item.EndDate >= start).ToList();
        return Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)result);
    }

    public Task<IEnumerable<ApartmentPriceCalendar>> GetExistingPriceCalendarRecordsAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
    {
        var result = _items.Where(item => item.ApartmentId == apartmentId && item.StartDate <= endDate && item.EndDate >= startDate);
        return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(result);
    }

    public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingStandardRecords(Guid apartmentId, DateOnly start, DateOnly end)
    {
        var result = _items.Where(item => item.ApartmentId == apartmentId && item.StartDate <= end && item.EndDate >= start).ToList();
        return Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)result);
    }

    public Task<IEnumerable<ApartmentPriceCalendar>> GetPriceCalendarRecordsForDeletionAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
    {
        var result = _items.Where(item => item.ApartmentId == apartmentId && item.StartDate <= endDate && item.EndDate >= startDate);
        return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(result);
    }

    public void Remove(ApartmentPriceCalendar entity)
    {
        _items.RemoveAll(item => item.PriceId == entity.PriceId);
    }

    public void Update(ApartmentPriceCalendar entity)
    {
        Remove(entity);
        _items.Add(entity);
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

// (Duplicate in-memory apartment repository removed; single implementation remains later in file.)

internal sealed class InMemoryApartmentRepository : IApartmentRepository
{
    private readonly List<Apartment> _apartments;

    public InMemoryApartmentRepository(params Apartment[] apartments)
    {
        _apartments = apartments.ToList();
    }

    public Task AddAsync(Apartment entity)
    {
        _apartments.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Apartment>> FindAsync(Expression<Func<Apartment, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<Apartment> result = _apartments.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<Apartment>> FindNoTrackingAsync(Expression<Func<Apartment, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<Apartment> result = _apartments.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((_apartments.AsEnumerable(), _apartments.Count));
    }

    public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null)
    {
        IEnumerable<Apartment> result = _apartments.Where(a =>
            a.Status == null || !string.Equals(a.Status, "draft", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult((result, result.Count()));
    }

    public Task<Apartment?> GetByIdAsync(Guid id)
    {
        Apartment? result = _apartments.FirstOrDefault(a => a.ApartmentId == id);
        return Task.FromResult(result);
    }

    public Task<Apartment?> GetApartmentWithDetailsAsync(Guid id)
    {
        return GetByIdAsync(id);
    }

    public Task UpdateListingStatusAsync(Guid apartmentId, string status, string bookingStatus)
    {
        Apartment? apartment = _apartments.FirstOrDefault(a => a.ApartmentId == apartmentId);
        if (apartment != null)
        {
            apartment.Status = status;
            apartment.BookingStatus = bookingStatus;
        }

        return Task.CompletedTask;
    }

    public void Remove(Apartment entity)
    {
        _apartments.Remove(entity);
    }

    public void Update(Apartment entity)
    {
        // No special handling needed for in-memory list
    }

    public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        IEnumerable<string>? allowedColumns = null,
        Dictionary<string, string>? filters = null)
    {
        return Task.FromResult((_apartments.AsEnumerable(), _apartments.Count));
    }

    public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(
        int page,
        int pageSize,
        Guid landlordId,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        IEnumerable<string>? allowedColumns = null,
        Dictionary<string, string>? filters = null)
    {
        var items = _apartments.Where(a => a.LandlordId == landlordId);
        return Task.FromResult((items, items.Count()));
    }

    public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetApartmentByLandlordIdAsync(Guid landlordId, int page, int pageSize, string? sortBy, string? sortOrder, string? search, Dictionary<string, string>? filters, string[] allowedColumns)
    {
        var items = _apartments.Where(a => a.LandlordId == landlordId);
        return Task.FromResult((items, items.Count()));
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

internal sealed class InMemoryHolidaysEventRepository : IHolidaysEventRepository
{
    private readonly List<HolidaysEvent> _events;

    public InMemoryHolidaysEventRepository()
    {
        _events = new List<HolidaysEvent>();
    }

    public InMemoryHolidaysEventRepository(IEnumerable<HolidaysEvent> events)
    {
        _events = events.ToList();
    }

    public Task AddAsync(HolidaysEvent entity)
    {
        _events.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<HolidaysEvent>> FindAsync(Expression<Func<HolidaysEvent, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<HolidaysEvent> result = _events.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<HolidaysEvent>> FindNoTrackingAsync(Expression<Func<HolidaysEvent, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<HolidaysEvent> result = _events.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<(IEnumerable<HolidaysEvent> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((_events.AsEnumerable(), _events.Count));
    }

    public Task<HolidaysEvent?> GetByIdAsync(Guid id)
    {
        HolidaysEvent? result = _events.FirstOrDefault(e => e.EventId == id);
        return Task.FromResult(result);
    }

    public void Remove(HolidaysEvent entity)
    {
        _events.Remove(entity);
    }

    public void Update(HolidaysEvent entity)
    {
        // No special handling needed for in-memory list
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

internal sealed class InMemoryNearbyAttractionRepository : INearbyAttractionRepository
{
    private readonly List<NearbyAttraction> _attractions;

    public InMemoryNearbyAttractionRepository()
    {
        _attractions = new List<NearbyAttraction>();
    }

    public InMemoryNearbyAttractionRepository(IEnumerable<NearbyAttraction> attractions)
    {
        _attractions = attractions.ToList();
    }

    public Task AddAsync(NearbyAttraction entity)
    {
        _attractions.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<NearbyAttraction>> FindAsync(Expression<Func<NearbyAttraction, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<NearbyAttraction> result = _attractions.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<NearbyAttraction>> FindNoTrackingAsync(Expression<Func<NearbyAttraction, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<NearbyAttraction> result = _attractions.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<(IEnumerable<NearbyAttraction> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((_attractions.AsEnumerable(), _attractions.Count));
    }

    public Task<NearbyAttraction?> GetByIdAsync(Guid id)
    {
        NearbyAttraction? result = _attractions.FirstOrDefault(a => a.AttractionId == id);
        return Task.FromResult(result);
    }

    public void Remove(NearbyAttraction entity)
    {
        _attractions.Remove(entity);
    }

    public void Update(NearbyAttraction entity)
    {
        // No special handling needed for in-memory list
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}
