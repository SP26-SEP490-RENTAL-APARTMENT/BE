using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class ApartmentPriceCalendarSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var apartment = await context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId != Guid.Empty, cancellationToken);
        if (apartment is null)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var priceCalendars = new List<ApartmentPriceCalendar>
        {
            new()
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                StartDate = today.AddDays(14),
                EndDate = today.AddDays(21),
                DiscountPercentage = 15m,
                IsDiscount = true,
                PriceType = "weekly_discount",
                MinNights = 7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                StartDate = today.AddDays(30),
                EndDate = today.AddDays(60),
                DiscountPercentage = 25m,
                IsDiscount = true,
                PriceType = "monthly_discount",
                MinNights = 30,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                StartDate = today.AddDays(7),
                EndDate = today.AddDays(10),
                DiscountPercentage = 10m,
                IsDiscount = false,
                PriceType = "surge_pricing",
                MinNights = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var priceCalendar in priceCalendars)
        {
            if (!await context.ApartmentPriceCalendars.AnyAsync(p => p.PriceId == priceCalendar.PriceId, cancellationToken))
            {
                context.ApartmentPriceCalendars.Add(priceCalendar);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
