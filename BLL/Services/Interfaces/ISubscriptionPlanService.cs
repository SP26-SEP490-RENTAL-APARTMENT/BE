using DAL.Models;

namespace BLL.Services.Interfaces
{
    public interface ISubscriptionPlanService : IBaseService<SubscriptionPlan>
    {
        public Task<(IEnumerable<SubscriptionPlan> Items, int TotalCount)> GetAllForLandlordAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null);
    }
}