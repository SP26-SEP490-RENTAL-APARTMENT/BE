using System.Diagnostics;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Implements;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

Console.WriteLine("Seeding in-memory DB and validating BookingRepository grouping...");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("BookingGroupValidatorDb")
    .Options;

using var ctx = new AppDbContext(options);

// Seed apartments and tenants
var aptCount = 100;
var tenantsPerApt = 5;
var bookingsPerTenant = 20;
var rng = new Random(123);

for (int a = 0; a < aptCount; a++)
{
    var aptId = Guid.NewGuid();
    ctx.Apartments.Add(new Apartment { ApartmentId = aptId, Title = $"Apt-{a}", LandlordId = Guid.NewGuid(), BasePricePerNight = 100m + a, Location = new Point(0,0) });
    for (int t = 0; t < tenantsPerApt; t++)
    {
        var tenantId = Guid.NewGuid();
        var user = new User { UserId = tenantId, FullName = $"Tenant-{a}-{t}", Email = $"tenant{a}{t}@example.com", PasswordHash = "pwd", Role = "tenant" };
        ctx.Users.Add(user);
        ctx.Tenants.Add(new Tenant { TenantId = tenantId, TenantNavigation = user });
        for (int b = 0; b < bookingsPerTenant; b++)
        {
            var created = DateTime.UtcNow.AddDays(-rng.Next(0, 60));
            ctx.Bookings.Add(new Booking
            {
                BookingId = Guid.NewGuid(),
                ApartmentId = aptId,
                TenantId = tenantId,
                CreatedAt = created,
                Nights = rng.Next(1, 7),
                TotalPrice = 100m * rng.Next(1, 7),
                Status = rng.NextDouble() > 0.2 ? "paid" : "pending",
                PaymentMode = rng.NextDouble() > 0.5 ? "card" : "cash",
                PackageId = null,
                PackagePrice = null,
                AmountPaid = 0m
            });
        }
    }
}
await ctx.SaveChangesAsync();

// Seed some reviews
for (int a = 0; a < aptCount; a++)
{
    var apt = ctx.Apartments.Skip(a).First();
    var firstBooking = ctx.Bookings.First(b => b.ApartmentId == apt.ApartmentId);
    for (int r = 0; r < 3; r++)
    {
        ctx.Reviews.Add(new Review
        {
            ReviewId = Guid.NewGuid(),
            ApartmentId = apt.ApartmentId,
            BookingId = firstBooking.BookingId,
            ReviewerId = firstBooking.TenantId,
            ReviewedId = firstBooking.TenantId,
            Rating = (sbyte)(3 + (r % 3)),
            CreatedAt = DateTime.UtcNow.AddDays(-r * 5)
        });
    }
}
await ctx.SaveChangesAsync();

// Seed smart pricing and price calendars for base-price validation
foreach (var apt in ctx.Apartments)
{
    for (int d = 0; d < 10; d++)
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-d));
        if (apt.ApartmentId.GetHashCode() % 2 == 0)
        {
            ctx.SmartPricingHistories.Add(new SmartPricingHistory
            {
                PricingId = Guid.NewGuid(),
                ApartmentId = apt.ApartmentId,
                Date = day,
                BasePrice = apt.BasePricePerNight + 10m,
                SuggestedPrice = apt.BasePricePerNight + 15m,
                Multiplier = 1.1m,
                CreatedAt = DateTime.UtcNow.AddDays(-d)
            });
        }
        else
        {
            ctx.ApartmentPriceCalendars.Add(new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apt.ApartmentId,
                PricingPolicyId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                VersionNumber = 1,
                StartDate = day,
                EndDate = day,
                FixedPricePerNight = apt.BasePricePerNight + 20m,
                PriceType = "manual",
                CreatedAt = DateTime.UtcNow.AddDays(-d)
            });
        }
    }
}
await ctx.SaveChangesAsync();

// Seed overlapping availability ranges to validate distinct blocked-day counting
var firstApartment = ctx.Apartments.First();
ctx.ApartmentAvailabilities.Add(new ApartmentAvailability
{
    AvailabilityId = Guid.NewGuid(),
    ApartmentId = firstApartment.ApartmentId,
    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-9)),
    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-5)),
    Reason = "maintenance",
    CreatedAt = DateTime.UtcNow
});
ctx.ApartmentAvailabilities.Add(new ApartmentAvailability
{
    AvailabilityId = Guid.NewGuid(),
    ApartmentId = firstApartment.ApartmentId,
    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7)),
    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3)),
    Reason = "cleaning",
    CreatedAt = DateTime.UtcNow
});
await ctx.SaveChangesAsync();

var repo = new BookingRepository(ctx);

var sw = Stopwatch.StartNew();
var (rows, total) = await repo.GetPagedGroupedReportRowsAsync(DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(1),
    new List<Common.DTOs.ReportDimensionRequestDto> { new Common.DTOs.ReportDimensionRequestDto { Field = "apartment_id", Alias = "apartment_id" } },
    new List<Common.DTOs.ReportMetricRequestDto> { new Common.DTOs.ReportMetricRequestDto { Field = "booking_count", Aggregation = "count" } },
    null,
    1,
    1000);
sw.Stop();

Console.WriteLine($"Rows returned: {rows.Count()} Total groups: {total} Time ms: {sw.ElapsedMilliseconds}");
// Additional check: review metrics
var (reviewRows, reviewTotal) = await repo.GetPagedGroupedReportRowsAsync(DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(1),
    new List<Common.DTOs.ReportDimensionRequestDto> { new Common.DTOs.ReportDimensionRequestDto { Field = "apartment_id", Alias = "apartment_id" } },
    new List<Common.DTOs.ReportMetricRequestDto> {
        new Common.DTOs.ReportMetricRequestDto { Field = "review_count", Aggregation = "count" },
        new Common.DTOs.ReportMetricRequestDto { Field = "review_avg_rating", Aggregation = "avg" }
    },
    null,
    1,
    1000);

Console.WriteLine($"Review rows: {reviewRows.Count()} Total groups: {reviewTotal}");
foreach (var r in reviewRows.Take(5))
{
    Console.WriteLine($"Apt:{r.Dimensions["apartment_id"]}");
    foreach (var kv in r.Metrics)
    {
        Console.WriteLine($"  {kv.Key}: {kv.Value}");
    }
}

var (baseRows, baseTotal) = await repo.GetPagedGroupedReportRowsAsync(DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(1),
    new List<Common.DTOs.ReportDimensionRequestDto> { new Common.DTOs.ReportDimensionRequestDto { Field = "apartment_id", Alias = "apartment_id" } },
    new List<Common.DTOs.ReportMetricRequestDto> {
        new Common.DTOs.ReportMetricRequestDto { Field = "avg_base_price", Aggregation = "avg" },
        new Common.DTOs.ReportMetricRequestDto { Field = "avg_price_delta", Aggregation = "avg" }
    },
    null,
    1,
    1000);

Console.WriteLine($"Base-price rows: {baseRows.Count()} Total groups: {baseTotal}");
foreach (var r in baseRows.Take(5))
{
    Console.WriteLine($"Apt:{r.Dimensions["apartment_id"]}");
    foreach (var kv in r.Metrics)
    {
        Console.WriteLine($"  {kv.Key}: {kv.Value}");
    }
}

var (occupancyRows, occupancyTotal) = await repo.GetPagedGroupedReportRowsAsync(DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(1),
    new List<Common.DTOs.ReportDimensionRequestDto> { new Common.DTOs.ReportDimensionRequestDto { Field = "apartment_id", Alias = "apartment_id" } },
    new List<Common.DTOs.ReportMetricRequestDto> { new Common.DTOs.ReportMetricRequestDto { Field = "occupancy_percent", Aggregation = "avg" } },
    null,
    1,
    1000);

Console.WriteLine($"Occupancy rows: {occupancyRows.Count()} Total groups: {occupancyTotal}");
foreach (var r in occupancyRows.Take(3))
{
    Console.WriteLine($"Apt:{r.Dimensions["apartment_id"]}");
    foreach (var kv in r.Metrics)
    {
        Console.WriteLine($"  {kv.Key}: {kv.Value}");
    }
}

return 0;