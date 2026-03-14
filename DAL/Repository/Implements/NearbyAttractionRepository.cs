using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class NearbyAttractionRepository : Repository<NearbyAttraction>, INearbyAttractionRepository
    {
        public NearbyAttractionRepository(AppDbContext context) : base(context)
        {
        }
    }
}