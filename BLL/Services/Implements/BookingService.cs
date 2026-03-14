using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class BookingService : BaseService<Booking>, IBookingService
{
    public BookingService(IRepository<Booking> repository) : base(repository)
    {
    }
}
