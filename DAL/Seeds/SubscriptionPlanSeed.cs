using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class SubscriptionPlanSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var plans = new List<SubscriptionPlan>
        {
            new()
            {
                Name = "Starter",
                Description = "Starter plan for new hosts",
                PriceMonthly = 0m,
                PriceAnnual = 0m,
                MaxApartments = 1,
                Features = "Basic listing features",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                Name = "Pro",
                Description = "Pro plan for growing hosts",
                PriceMonthly = 499000m,
                PriceAnnual = 4990000m,
                MaxApartments = 10,
                Features = "Advanced analytics and priority support",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                Name = "Enterprise",
                Description = "Enterprise plan for agencies",
                PriceMonthly = 1499000m,
                PriceAnnual = 14990000m,
                MaxApartments = null,
                Features = "Custom integrations and dedicated support",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            }
        };

        foreach (var plan in plans)
        {
            var exists = await context.SubscriptionPlans.AnyAsync(p => p.Name == plan.Name, cancellationToken);
            if (exists)
            {
                continue;
            }

            plan.PlanId = Guid.NewGuid();
            context.SubscriptionPlans.Add(plan);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
