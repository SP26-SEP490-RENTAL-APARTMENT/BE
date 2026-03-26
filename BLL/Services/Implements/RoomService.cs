using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class RoomService(IRepository<Room> repository)
    : BaseService<Room>(repository), IRoomService
{
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
}
