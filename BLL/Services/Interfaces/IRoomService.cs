using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IRoomService : IBaseService<Room>
{
    Task<(IEnumerable<Room> Items, int TotalCount)> GetByApartmentIdAsync(
        Guid apartmentId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null
    );

    Task<(IEnumerable<Room> Items, int TotalCount)> GetByLandlordIdAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null
    );
}
