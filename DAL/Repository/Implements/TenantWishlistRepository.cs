using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class TenantWishlistRepository : Repository<TenantWishlist>, ITenantWishlistRepository
    {
        public TenantWishlistRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<TenantWishlist> Items, int TotalCount)> GetTenantWishlistAsync(
            Guid tenantId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            Dictionary<string, string>? filters = null)
        {
            var query = _context.TenantWishlists
                .Where(w => w.TenantId == tenantId)
                .Include(w => w.Apartment)
                    .ThenInclude(a => a!.Room)
                .Include(w => w.Apartment)
                    .ThenInclude(a => a!.ApartmentMedia)
                .AsQueryable();

            // Apply price range filters
            if (priceMin.HasValue)
            {
                query = query.Where(w => w.Apartment!.BasePricePerNight >= priceMin.Value);
            }

            if (priceMax.HasValue)
            {
                query = query.Where(w => w.Apartment!.BasePricePerNight <= priceMax.Value);
            }

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "price" or "basepricepernight" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(w => w.Apartment!.BasePricePerNight)
                    : query.OrderBy(w => w.Apartment!.BasePricePerNight),
                "favorite" or "isfavorite" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(w => w.IsFavorite).ThenByDescending(w => w.CreatedAt)
                    : query.OrderBy(w => w.IsFavorite).ThenBy(w => w.CreatedAt),
                _ => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(w => w.CreatedAt)
                    : query.OrderBy(w => w.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId)
        {
            return await _context.TenantWishlists
                .AnyAsync(w => w.TenantId == tenantId && w.ApartmentId == apartmentId);
        }

        public async Task<int> GetWishlistCountAsync(Guid tenantId)
        {
            return await _context.TenantWishlists
                .CountAsync(w => w.TenantId == tenantId);
        }

        public async Task<TenantWishlist?> FindByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId)
        {
            return await _context.TenantWishlists
                .Include(w => w.Apartment)
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.ApartmentId == apartmentId);
        }

        public async Task<IEnumerable<TenantWishlist>> GetFavoritesOnlyAsync(Guid tenantId)
        {
            return await _context.TenantWishlists
                .Where(w => w.TenantId == tenantId && w.IsFavorite)
                .Include(w => w.Apartment)
                    .ThenInclude(a => a!.Room)
                .Include(w => w.Apartment)
                    .ThenInclude(a => a!.ApartmentMedia)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }
    }
}
