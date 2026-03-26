using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class SubscriptionPlanService : BaseService<SubscriptionPlan>, ISubscriptionPlanService
    {
        public SubscriptionPlanService(ISubscriptionPlanRepository repository) : base(repository)
        {
        }

        public override async Task<(IEnumerable<SubscriptionPlan> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var effectiveAllowedColumns = new[]
            {
                "PlanId",
                "Name",
                "Description",
                "PriceMonthly",
                "PriceAnnual",
                "IsActive",
                "CreatedAt"
            };

            return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
        }
    }
}