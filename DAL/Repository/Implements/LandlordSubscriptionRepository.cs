using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class LandlordSubscriptionRepository : Repository<LandlordSubscription>, ILandlordSubscriptionRepository
    {
        public LandlordSubscriptionRepository(AppDbContext context) : base(context)
        {
        }
    }
}