using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class PackageService : BaseService<Package>, IPackageService
{
    private readonly IRepository<PackageItem> _packageItemRepository;
    private readonly IRepository<PackagePackage> _packagePackageRepository;
        private readonly IPackageRepository _packageRepository;

    public PackageService(IRepository<Package> repository, IRepository<PackageItem> packageItemRepository, IRepository<PackagePackage> packagePackageRepository, IPackageRepository packageRepository)
        : base(repository)
    {
        _packageItemRepository = packageItemRepository;
        _packagePackageRepository = packagePackageRepository;
        _packageRepository = packageRepository;
    }

    public override async Task<Package?> GetByIdAsync(Guid id)
    {
        // use repository implementation that includes related PackageItems
        return await _packageRepository.GetByIdAsync(id);
    }

    public async Task<(IEnumerable<Package> Items, int TotalCount)> GetByApartmentIdAsync(
        Guid apartmentId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null)
    {
        var effectiveAllowedColumns = new[]
        {
            "PackageId",
            "ApartmentId",
            "Name",
            "Description",
            "Currency",
            "IsActive",
            "CreatedAt"
        };

        return await _packageRepository.GetByApartmentIdAsync(
            apartmentId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters,
            effectiveAllowedColumns);
    }

    public async Task AddItemsAsync(Guid packageId, List<Guid> packageItemIds)
    {
        // ensure package exists
        var pkg = await _packageRepository.GetByIdAsync(packageId);
        if (pkg == null)
            throw new ArgumentException($"Package with id '{packageId}' not found.");

        foreach (var itemId in packageItemIds)
        {
            var item = await _packageItemRepository.GetByIdAsync(itemId);
            if (item == null)
                throw new ArgumentException($"PackageItem with id '{itemId}' not found.");

            var link = new PackagePackage
            {
                Id = Guid.NewGuid(),
                PackageId = packageId,
                PackageItemId = itemId
            };

            await _packagePackageRepository.AddAsync(link);
        }

        await _packagePackageRepository.SaveChangesAsync();
    }

    public async Task RemoveItemAsync(Guid packageId, Guid packageItemId)
    {
        var pkg = await _packageRepository.GetByIdAsync(packageId);
        if (pkg == null)
            throw new ArgumentException($"Package with id '{packageId}' not found.");

        // find the link entity
        var links = await _packagePackageRepository.FindAsync(pp => pp.PackageId == packageId && pp.PackageItemId == packageItemId);
        var link = links.FirstOrDefault();
        if (link == null)
            throw new ArgumentException($"PackageItem with id '{packageItemId}' is not attached to package '{packageId}'.");

        _packagePackageRepository.Remove(link);
        await _packagePackageRepository.SaveChangesAsync();
    }

    public async Task<(IEnumerable<Package> Items, int TotalCount)> GetAllWithDetailsAsync(
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
            "PackageId",
            "ApartmentId",
            "Name",
            "Description",
            "Currency",
            "IsActive",
            "CreatedAt"
        };

        return await _packageRepository.GetAllWithDetailsAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
    }
}
