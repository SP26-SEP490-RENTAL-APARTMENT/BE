using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class LandlordSubscriptionService : BaseService<LandlordSubscription>, ILandlordSubscriptionService
{
    public LandlordSubscriptionService(IRepository<LandlordSubscription> repository) : base(repository)
    {
    }
}
