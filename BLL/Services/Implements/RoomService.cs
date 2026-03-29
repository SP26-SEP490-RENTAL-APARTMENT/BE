using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class RoomService(IRepository<Room> repository)
    : BaseService<Room>(repository), IRoomService
{
    private readonly IRepository<Room> _roomRepository = repository;

    public override async Task<(IEnumerable<Room> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        var effectiveAllowedColumns = new[]
        {
            "RoomId",
            "ApartmentId",
            "Title",
            "Description",
            "RoomType",
            "BedType",
            "CreatedAt"
        };

        return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
    }

    public async Task<Room?> GetByApartmentIdAsync(Guid apartmentId)
    {
        var rooms = await _roomRepository.FindAsync(r => r.ApartmentId == apartmentId);
        return rooms.FirstOrDefault();
    }

    public async Task<IEnumerable<Room>> GetByLandlordIdAsync(Guid landlordId)
    {
        var rooms = await _roomRepository.FindAsync(r => r.Apartment.LandlordId == landlordId);
        return rooms;
    }
}
