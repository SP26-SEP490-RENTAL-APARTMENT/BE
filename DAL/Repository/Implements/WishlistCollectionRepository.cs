using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class WishlistCollectionRepository : Repository<WishlistCollection>, IWishlistCollectionRepository
    {
        public WishlistCollectionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<WishlistCollection> Items, int TotalCount)> GetTenantCollectionsAsync(
            Guid tenantId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null)
        {
            var query = _context.WishlistCollections
                .Where(c => c.TenantId == tenantId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(normalizedSearch)
                    || (c.Description != null && c.Description.ToLower().Contains(normalizedSearch)));
            }

            if (filters != null)
            {
                if (filters.TryGetValue("isDefault", out var isDefaultValue) && bool.TryParse(isDefaultValue, out var isDefault))
                {
                    query = query.Where(c => c.IsDefault == isDefault);
                }

                if (filters.TryGetValue("name", out var nameFilter) && !string.IsNullOrWhiteSpace(nameFilter))
                {
                    var normalizedNameFilter = nameFilter.Trim().ToLower();
                    query = query.Where(c => c.Name.ToLower().Contains(normalizedNameFilter));
                }
            }

            var isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
            query = sortBy?.ToLower() switch
            {
                "name" => isDesc ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
                "createdat" => isDesc ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
                "updatedat" => isDesc ? query.OrderByDescending(c => c.UpdatedAt) : query.OrderBy(c => c.UpdatedAt),
                "isdefault" => isDesc
                    ? query.OrderByDescending(c => c.IsDefault).ThenByDescending(c => c.Name)
                    : query.OrderBy(c => c.IsDefault).ThenBy(c => c.Name),
                _ => query.OrderByDescending(c => c.IsDefault).ThenBy(c => c.Name)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<WishlistCollection?> GetDefaultCollectionAsync(Guid tenantId)
        {
            return await _context.WishlistCollections
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsDefault);
        }

        public async Task<WishlistCollection?> GetByIdForTenantAsync(Guid tenantId, Guid collectionId)
        {
            return await _context.WishlistCollections
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CollectionId == collectionId);
        }

        public async Task<bool> ExistsCollectionNameAsync(Guid tenantId, string normalizedName, Guid? excludedCollectionId = null)
        {
            return await _context.WishlistCollections
                .AnyAsync(c => c.TenantId == tenantId
                    && c.Name.ToLower() == normalizedName
                    && (!excludedCollectionId.HasValue || c.CollectionId != excludedCollectionId.Value));
        }
    }
}