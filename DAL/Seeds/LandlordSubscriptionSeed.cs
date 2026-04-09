using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class LandlordSubscriptionSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlordIds = await context.Landlords
            .Where(l => l.LandlordId != Guid.Empty)
            .Select(l => l.LandlordId)
            .ToListAsync(cancellationToken);

        if (landlordIds.Count == 0)
            return;

        var plan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId != Guid.Empty, cancellationToken);
        if (plan is null)
            return;

        var existingSubscriptionIds = await context.LandlordSubscriptions
            .Where(s => landlordIds.Contains(s.LandlordId))
            .Select(s => s.LandlordId)
            .ToListAsync(cancellationToken);

        var missingSubscriptions = landlordIds
            .Except(existingSubscriptionIds)
            .Select(landlordId =>
            {
                var startDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);
                return new LandlordSubscription
                {
                    SubscriptionId = Guid.NewGuid(),
                    LandlordId = landlordId,
                    PlanId = plan.PlanId,
                    Status = "active",
                    StartDate = startDate,
                    EndDate = startDate.AddMonths(1),
                    RenewalType = "monthly",
                    AutoRenew = true,
                    PaymentMethod = "credit_card",
                    LastPaymentId = null,
                    CreatedAt = Common.Utils.VietnamTime.Now,
                    UpdatedAt = Common.Utils.VietnamTime.Now
                };
            })
            .ToList();

        if (missingSubscriptions.Count == 0)
        {
            return;
        }

        context.LandlordSubscriptions.AddRange(missingSubscriptions);
        await context.SaveChangesAsync(cancellationToken);
    }
}
