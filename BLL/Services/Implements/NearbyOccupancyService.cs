using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services.Implements;

public sealed class NearbyOccupancyService : INearbyOccupancyService, IOccupancyAggregationService
{
    private const decimal DefaultBaselineOccupancy = 0.55m;
    private const int DefaultHistoryLookbackDays = 30;
    private const double DefaultNearbyRadiusKilometers = 3.0d;
    private const int DefaultCacheMinutes = 30;
    private static readonly string[] OccupiedStatuses = ["confirmed", "paid", "completed", "disputed"];

    private readonly AppDbContext _dbContext;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly ICacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NearbyOccupancyService> _logger;

    public NearbyOccupancyService(
        AppDbContext dbContext,
        IApartmentRepository apartmentRepository,
        ICacheService cacheService,
        IConfiguration configuration,
        ILogger<NearbyOccupancyService> logger)
    {
        _dbContext = dbContext;
        _apartmentRepository = apartmentRepository;
        _cacheService = cacheService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<decimal> GetOccupancyRateAsync(
        Guid apartmentId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date cannot be after end date.");

        var total = 0m;
        var dayCount = 0;

        for (var current = startDate; current <= endDate; current = current.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += await GetDailyOccupancyRateAsync(apartmentId, current, cancellationToken);
            dayCount++;
        }

        if (dayCount == 0)
            return DefaultBaselineOccupancy;

        return ClampOccupancy(Math.Round(total / dayCount, 4, MidpointRounding.AwayFromZero));
    }

    public async Task RefreshCacheAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date cannot be after end date.");

        var apartments = (await _apartmentRepository.FindNoTrackingAsync(a =>
            a.Status != null && a.Status == "posted" && a.Location != null)).ToList();

        foreach (var apartment in apartments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var current = startDate; current <= endDate; current = current.AddDays(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var occupancyRate = await CalculateAndCacheDailyOccupancyRateAsync(apartment, current, cancellationToken);
                _logger.LogDebug(
                    "Cached occupancy rate {OccupancyRate} for apartment {ApartmentId} on {Date}",
                    occupancyRate,
                    apartment.ApartmentId,
                    current);
            }
        }
    }

    private async Task<decimal> GetDailyOccupancyRateAsync(
        Guid apartmentId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(apartmentId, date);
        var cachedRate = await _cacheService.GetAsync<decimal?>(cacheKey);
        if (cachedRate.HasValue)
            return ClampOccupancy(cachedRate.Value);

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null || apartment.Location == null)
            return DefaultBaselineOccupancy;

        return await CalculateAndCacheDailyOccupancyRateAsync(apartment, date, cancellationToken);
    }

    private async Task<decimal> CalculateAndCacheDailyOccupancyRateAsync(
        Apartment apartment,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(apartment.ApartmentId, date);
        var occupancyRate = await CalculateDailyOccupancyRateAsync(apartment, date, cancellationToken);
        await _cacheService.SetAsync(cacheKey, occupancyRate, TimeSpan.FromMinutes(GetCacheMinutes()));
        return occupancyRate;
    }

    private async Task<decimal> CalculateDailyOccupancyRateAsync(
        Apartment apartment,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (apartment.Location == null)
            return DefaultBaselineOccupancy;

        var lookbackDays = GetHistoryLookbackDays();
        if (lookbackDays <= 1)
        {
            return await CalculateSnapshotOccupancyRateAsync(apartment, date, cancellationToken);
        }

        var startDate = date.AddDays(-(lookbackDays - 1));
        decimal weightedTotal = 0m;
        decimal weightTotal = 0m;

        for (var offset = 0; offset < lookbackDays; offset++)
        {
            var historyDate = startDate.AddDays(offset);
            var weight = offset + 1;
            var snapshotRate = await CalculateSnapshotOccupancyRateAsync(apartment, historyDate, cancellationToken);

            weightedTotal += snapshotRate * weight;
            weightTotal += weight;
        }

        if (weightTotal == 0m)
            return DefaultBaselineOccupancy;

        return ClampOccupancy(Math.Round(weightedTotal / weightTotal, 4, MidpointRounding.AwayFromZero));
    }

    private async Task<decimal> CalculateSnapshotOccupancyRateAsync(
        Apartment apartment,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var nearbyApartmentIds = await GetNearbyApartmentIdsAsync(apartment, cancellationToken);
        if (nearbyApartmentIds.Count == 0)
            return DefaultBaselineOccupancy;

        var bookedApartmentIds = await _dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                nearbyApartmentIds.Contains(booking.ApartmentId) &&
                booking.Status != null &&
                OccupiedStatuses.Contains(booking.Status.ToLower()) &&
                booking.CheckInDate <= date &&
                booking.CheckOutDate > date)
            .Select(booking => booking.ApartmentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rate = (decimal)bookedApartmentIds.Count / nearbyApartmentIds.Count;
        return ClampOccupancy(rate);
    }

    private async Task<List<Guid>> GetNearbyApartmentIdsAsync(Apartment apartment, CancellationToken cancellationToken)
    {
        if (apartment.Location == null)
            return [];

        var radiusMeters = GetNearbyRadiusKilometers() * 1000d;
        var query = _dbContext.Apartments
            .AsNoTracking()
            .Where(candidate =>
                candidate.ApartmentId != apartment.ApartmentId &&
                candidate.Status != null &&
                candidate.Status == "posted" &&
                candidate.Location != null &&
                candidate.Location.Distance(apartment.Location) <= radiusMeters);

        return await query
            .Select(candidate => candidate.ApartmentId)
            .ToListAsync(cancellationToken);
    }

    private static string BuildCacheKey(Guid apartmentId, DateOnly date)
        => $"occupancy:{apartmentId:N}:{date:yyyyMMdd}";

    private double GetNearbyRadiusKilometers()
        => _configuration.GetValue("OccupancyPricing:NearbyRadiusKm", DefaultNearbyRadiusKilometers);

    private int GetCacheMinutes()
        => _configuration.GetValue("OccupancyPricing:CacheMinutes", DefaultCacheMinutes);

    private int GetHistoryLookbackDays()
        => Math.Max(1, _configuration.GetValue("OccupancyPricing:HistoryLookbackDays", DefaultHistoryLookbackDays));

    private static decimal ClampOccupancy(decimal occupancyRate)
        => Math.Clamp(occupancyRate, 0m, 1m);
}