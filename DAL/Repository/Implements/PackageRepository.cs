using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class PackageRepository : Repository<Package>, IPackageRepository
    {
        public PackageRepository(AppDbContext context) : base(context)
        {
        }

        public override async Task<Package?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(p => p.PackagePackages)
                    .ThenInclude(pp => pp.PackageItem)
                .FirstOrDefaultAsync(p => p.PackageId == id);
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
            var query = _dbSet.AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(p => p.PackagePackages)
                    .ThenInclude(pp => pp.PackageItem)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
