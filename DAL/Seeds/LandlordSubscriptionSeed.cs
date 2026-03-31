using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class LandlordSubscriptionSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlord = await context.Landlords.SingleOrDefaultAsync(l => l.LandlordId != Guid.Empty, cancellationToken);
        if (landlord is null)
            return;

        var plan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId != Guid.Empty, cancellationToken);
        if (plan is null)
            return;

        if (!await context.LandlordSubscriptions.AnyAsync(s => s.LandlordId == landlord.LandlordId, cancellationToken))
        {
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
            context.LandlordSubscriptions.Add(new LandlordSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                LandlordId = landlord.LandlordId,
                PlanId = plan.PlanId,
                Status = "active",
                StartDate = startDate,
                EndDate = startDate.AddMonths(1),
                RenewalType = "monthly",
                AutoRenew = true,
                PaymentMethod = "credit_card",
                LastPaymentId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
