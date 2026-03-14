using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ApartmentPriceCalendarService : BaseService<ApartmentPriceCalendar>, IApartmentPriceCalendarService
{
    public ApartmentPriceCalendarService(IRepository<ApartmentPriceCalendar> repository) : base(repository)
    {
    }
}
