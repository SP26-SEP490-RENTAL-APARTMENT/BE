using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class ApartmentPriceCalendarRepository : Repository<ApartmentPriceCalendar>, IApartmentPriceCalendarRepository
    {
        public ApartmentPriceCalendarRepository(AppDbContext context) : base(context)
        {
        }
    }
}