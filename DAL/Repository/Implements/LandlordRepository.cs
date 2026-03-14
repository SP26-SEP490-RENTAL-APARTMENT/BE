using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class LandlordRepository : Repository<Landlord>, ILandlordRepository
    {
        public LandlordRepository(AppDbContext context) : base(context)
        {
        }
    }
}