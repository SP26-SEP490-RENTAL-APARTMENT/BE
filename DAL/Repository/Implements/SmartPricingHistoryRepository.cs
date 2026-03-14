using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class SmartPricingHistoryRepository : Repository<SmartPricingHistory>, ISmartPricingHistoryRepository
    {
        public SmartPricingHistoryRepository(AppDbContext context) : base(context)
        {
        }
    }
}