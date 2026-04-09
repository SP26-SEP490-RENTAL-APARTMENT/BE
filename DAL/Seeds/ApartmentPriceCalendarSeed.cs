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

        var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);

        var priceCalendars = new List<ApartmentPriceCalendar>
        {
            new()
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                StartDate = today.AddDays(14),
                EndDate = today.AddDays(20),
                DiscountPercentage = 15m,
                IsDiscount = true,
                PriceType = "low_season",
                MinNights = 7,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                StartDate = today.AddDays(30),
                EndDate = today.AddDays(60),
                DiscountPercentage = 25m,
                IsDiscount = true,
                PriceType = "low_season",
                MinNights = 30,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
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
