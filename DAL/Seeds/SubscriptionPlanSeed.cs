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
                NameVi = "Cơ bản",
                Description = "Starter plan for new hosts",
                DescriptionVi = "Gói cơ bản dành cho chủ nhà mới",
                PriceMonthly = 0m,
                PriceAnnual = 0m,
                MaxApartments = 1,
                Features = "Basic listing features",
                FeaturesVi = "Tính năng đăng tin cơ bản",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                Name = "Pro",
                NameVi = "Chuyên nghiệp",
                Description = "Pro plan for growing hosts",
                DescriptionVi = "Gói chuyên nghiệp dành cho chủ nhà đang phát triển",
                PriceMonthly = 499000m,
                PriceAnnual = 4990000m,
                MaxApartments = 10,
                Features = "Advanced analytics and priority support",
                FeaturesVi = "Phân tích nâng cao và hỗ trợ ưu tiên",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                Name = "Enterprise",
                NameVi = "Doanh nghiệp",
                Description = "Enterprise plan for agencies",
                DescriptionVi = "Gói doanh nghiệp dành cho các đại lý",
                PriceMonthly = 1499000m,
                PriceAnnual = 14990000m,
                MaxApartments = null,
                Features = "Custom integrations and dedicated support",
                FeaturesVi = "Tích hợp tùy chỉnh và hỗ trợ chuyên biệt",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            }
        };

        foreach (var plan in plans)
        {
            var existing = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == plan.Name, cancellationToken);
            if (existing is not null)
            {
                existing.NameVi = plan.NameVi;
                existing.DescriptionVi = plan.DescriptionVi;
                existing.FeaturesVi = plan.FeaturesVi;
                context.SubscriptionPlans.Update(existing);
                continue;
            }

            plan.PlanId = Guid.NewGuid();
            context.SubscriptionPlans.Add(plan);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
