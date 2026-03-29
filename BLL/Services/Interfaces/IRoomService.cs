using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IRoomService : IBaseService<Room>
{
    Task<Room?> GetByApartmentIdAsync(Guid apartmentId);
    Task<IEnumerable<Room>> GetByLandlordIdAsync(Guid landlordId);
}
