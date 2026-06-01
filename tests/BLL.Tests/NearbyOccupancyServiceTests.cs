using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLL.Services.Implements;
using Common.Settings;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Implements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;

namespace BLL.Tests;

public class NearbyOccupancyServiceTests
{
    [Fact]
    public async Task GetOccupancyRateAsync_WeightsRecentHistoryMoreThanOlderHistory()
    {
        var targetApartmentId = Guid.NewGuid();
        var nearbyApartmentId = Guid.NewGuid();
        var targetDate = new DateOnly(2026, 6, 1);
        var lookbackDays = 30;

        var db = CreateDbContext(targetApartmentId, nearbyApartmentId, targetDate, recentBookedFromOffset: 15);
        var apartmentRepository = new ApartmentRepository(db);
        var cacheService = new NoOpCacheService();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OccupancyPricing:HistoryLookbackDays"] = lookbackDays.ToString(),
                ["OccupancyPricing:NearbyRadiusKm"] = "5"
            })
            .Build();

        var service = new NearbyOccupancyService(
            db,
            apartmentRepository,
            cacheService,
            configuration,
            NullLogger<NearbyOccupancyService>.Instance);

        var occupancyRate = await service.GetOccupancyRateAsync(targetApartmentId, targetDate, targetDate);

        var expectedRate = CalculateExpectedWeightedRate(lookbackDays, bookedWeightStartOffset: 14);

        Assert.Equal(expectedRate, occupancyRate);
    }

    private static AppDbContext CreateDbContext(
        Guid targetApartmentId,
        Guid nearbyApartmentId,
        DateOnly targetDate,
        int recentBookedFromOffset)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new AppDbContext(options);
        var apartmentLocation = new Point(0, 0) { SRID = 4326 };

        db.Apartments.AddRange(
            new Apartment
            {
                ApartmentId = targetApartmentId,
                LandlordId = Guid.NewGuid(),
                Title = "Target Apartment",
                City = "Hanoi",
                Location = apartmentLocation,
                BasePricePerNight = 100m,
                Status = "posted",
                BookingStatus = "available",
                CreatedAt = DateTime.UtcNow
            },
            new Apartment
            {
                ApartmentId = nearbyApartmentId,
                LandlordId = Guid.NewGuid(),
                Title = "Nearby Apartment",
                City = "Hanoi",
                Location = new Point(0, 0) { SRID = 4326 },
                BasePricePerNight = 120m,
                Status = "posted",
                BookingStatus = "available",
                CreatedAt = DateTime.UtcNow
            });

        db.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApartmentId = nearbyApartmentId,
            CheckInDate = targetDate.AddDays(-recentBookedFromOffset),
            CheckOutDate = targetDate.AddDays(1),
            Nights = recentBookedFromOffset + 1,
            TotalPrice = 0m,
            AmountPaid = 0m,
            RemainingAmount = 0m,
            DepositAmount = 0m,
            UpfrontPaymentAmount = 0m,
            BalanceDueDate = targetDate,
            Status = "confirmed",
            CreatedAt = DateTime.UtcNow
        });

        db.SaveChanges();
        return db;
    }

    private static decimal CalculateExpectedWeightedRate(int lookbackDays, int bookedWeightStartOffset)
    {
        decimal weightedTotal = 0m;
        decimal weightTotal = 0m;

        for (var offset = 0; offset < lookbackDays; offset++)
        {
            var weight = offset + 1;
            var booked = offset >= bookedWeightStartOffset;
            weightedTotal += booked ? weight : 0m;
            weightTotal += weight;
        }

        return Math.Round(weightedTotal / weightTotal, 4, MidpointRounding.AwayFromZero);
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan expiry) => Task.CompletedTask;
    }
}
