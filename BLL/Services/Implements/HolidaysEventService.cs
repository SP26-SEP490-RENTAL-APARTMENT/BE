using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class HolidaysEventService : BaseService<HolidaysEvent>, IHolidaysEventService
{
    public HolidaysEventService(IRepository<HolidaysEvent> repository) : base(repository)
    {
    }
}
