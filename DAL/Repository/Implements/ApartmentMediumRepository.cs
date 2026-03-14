using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class ApartmentMediumRepository : Repository<ApartmentMedium>, IApartmentMediumRepository
    {
        public ApartmentMediumRepository(AppDbContext context) : base(context)
        {
        }
    }
}