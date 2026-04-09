using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class SmartPricingHistorySeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var apartment = await context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId != Guid.Empty, cancellationToken);
        if (apartment is null)
            return;

        var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);

        var pricingHistories = new List<SmartPricingHistory>
        {
            new()
            {
                PricingId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                Date = today,
                SuggestedPrice = apartment.BasePricePerNight * 1.1m,
                BasePrice = apartment.BasePricePerNight,
                Multiplier = 1.1m,
                Reason = "High demand period - weekend",
                OccupancyRate = 0.85m,
                AcceptedByLandlord = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                PricingId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                Date = today.AddDays(1),
                SuggestedPrice = apartment.BasePricePerNight,
                BasePrice = apartment.BasePricePerNight,
                Multiplier = 1.0m,
                Reason = "Standard pricing",
                OccupancyRate = 0.50m,
                AcceptedByLandlord = null,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                PricingId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                Date = today.AddDays(7),
                SuggestedPrice = apartment.BasePricePerNight * 0.9m,
                BasePrice = apartment.BasePricePerNight,
                Multiplier = 0.9m,
                Reason = "Low demand period - midweek",
                OccupancyRate = 0.30m,
                AcceptedByLandlord = false,
                CreatedAt = Common.Utils.VietnamTime.Now.AddDays(-1)
            }
        };

        foreach (var pricing in pricingHistories)
        {
            if (!await context.SmartPricingHistories.AnyAsync(p => p.PricingId == pricing.PricingId, cancellationToken))
            {
                context.SmartPricingHistories.Add(pricing);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
