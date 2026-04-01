using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class RoomService(IRoomRepository repository)
    : BaseService<Room>(repository), IRoomService
{
    private readonly IRoomRepository _roomRepository = repository;

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

    public async Task<(IEnumerable<Room> Items, int TotalCount)> GetByApartmentIdAsync(
        Guid apartmentId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null
    )
    {
        var allowedColumns = new[]
        {
            "RoomId",
            "ApartmentId",
            "Title",
            "Description",
            "RoomType",
            "BedType",
            "CreatedAt"
        };

        return await _roomRepository.GetByApartmentIdAsync(
            apartmentId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters,
            allowedColumns
        );
    }

    public async Task<(IEnumerable<Room> Items, int TotalCount)> GetByLandlordIdAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null
    )
    {
        var allowedColumns = new[]
        {
            "RoomId",
            "ApartmentId",
            "Title",
            "Description",
            "RoomType",
            "BedType",
            "CreatedAt"
        };

        return await _roomRepository.GetByLandlordIdAsync(
            landlordId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters,
            allowedColumns
        );
    }
}
