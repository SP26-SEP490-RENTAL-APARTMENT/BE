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
    }
}