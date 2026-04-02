using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IPackageService : IBaseService<Package>
{
    Task<(IEnumerable<Package> Items, int TotalCount)> GetByApartmentIdAsync(
        Guid apartmentId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null);

    Task AddItemsAsync(Guid packageId, List<Guid> packageItemIds);
    Task RemoveItemAsync(Guid packageId, Guid packageItemId);

    Task<(IEnumerable<Package> Items, int TotalCount)> GetAllWithDetailsAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null);
}
