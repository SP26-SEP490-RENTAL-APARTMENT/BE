using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class SubscriptionPlanRepository : Repository<SubscriptionPlan>, ISubscriptionPlanRepository
    {
        public SubscriptionPlanRepository(AppDbContext context) : base(context)
        {
        }
    }
}