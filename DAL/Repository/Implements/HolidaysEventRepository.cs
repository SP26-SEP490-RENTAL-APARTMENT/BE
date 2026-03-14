using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class HolidaysEventRepository : Repository<HolidaysEvent>, IHolidaysEventRepository
    {
        public HolidaysEventRepository(AppDbContext context) : base(context)
        {
        }
    }
}