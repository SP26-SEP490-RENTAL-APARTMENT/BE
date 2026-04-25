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
            string? search = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            Guid? collectionId = null,
            Dictionary<string, string>? filters = null)
        {
            var query = _context.TenantWishlists
                .Where(w => w.TenantId == tenantId)
                .Where(w => !collectionId.HasValue || w.CollectionId == collectionId.Value)
                .Include(w => w.Collection)
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

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(w =>
                    (w.Notes != null && w.Notes.ToLower().Contains(normalizedSearch)) ||
                    (w.Collection.Name != null && w.Collection.Name.ToLower().Contains(normalizedSearch)) ||
                    (w.Apartment != null &&
                        ((w.Apartment.Title != null && w.Apartment.Title.ToLower().Contains(normalizedSearch)) ||
                         (w.Apartment.Address != null && w.Apartment.Address.ToLower().Contains(normalizedSearch)) ||
                         (w.Apartment.City != null && w.Apartment.City.ToLower().Contains(normalizedSearch)) ||
                         (w.Apartment.District != null && w.Apartment.District.ToLower().Contains(normalizedSearch)))));
            }

            if (filters != null)
            {
                if (filters.TryGetValue("isFavorite", out var isFavoriteValue) && bool.TryParse(isFavoriteValue, out var isFavorite))
                {
                    query = query.Where(w => w.IsFavorite == isFavorite);
                }

                if (filters.TryGetValue("status", out var status) && !string.IsNullOrWhiteSpace(status))
                {
                    var normalizedStatus = status.Trim().ToLower();
                    query = query.Where(w => w.Apartment != null && w.Apartment.Status != null && w.Apartment.Status.ToLower() == normalizedStatus);
                }

                if (filters.TryGetValue("city", out var city) && !string.IsNullOrWhiteSpace(city))
                {
                    var normalizedCity = city.Trim().ToLower();
                    query = query.Where(w => w.Apartment != null && w.Apartment.City != null && w.Apartment.City.ToLower() == normalizedCity);
                }

                if (filters.TryGetValue("district", out var district) && !string.IsNullOrWhiteSpace(district))
                {
                    var normalizedDistrict = district.Trim().ToLower();
                    query = query.Where(w => w.Apartment != null && w.Apartment.District != null && w.Apartment.District.ToLower() == normalizedDistrict);
                }
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

        public async Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid collectionId)
        {
            return await _context.TenantWishlists
            .AnyAsync(w => w.TenantId == tenantId && w.ApartmentId == apartmentId && w.CollectionId == collectionId);
        }

        public async Task<IEnumerable<TenantWishlist>> GetByTenantAndApartmentIdsAsync(Guid tenantId, IEnumerable<Guid> apartmentIds)
        {
            var apartmentIdSet = apartmentIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (apartmentIdSet.Count == 0)
            {
                return Enumerable.Empty<TenantWishlist>();
            }

            return await _context.TenantWishlists
                .Where(w => w.TenantId == tenantId && apartmentIdSet.Contains(w.ApartmentId))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetWishlistCountAsync(Guid tenantId)
        {
            return await _context.TenantWishlists
                .CountAsync(w => w.TenantId == tenantId);
        }

        public async Task<TenantWishlist?> FindByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId, Guid collectionId)
        {
            return await _context.TenantWishlists
                .Include(w => w.Collection)
                .Include(w => w.Apartment)
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.ApartmentId == apartmentId && w.CollectionId == collectionId);
        }

        public async Task<IEnumerable<TenantWishlist>> FindAllByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId)
        {
            return await _context.TenantWishlists
                .Where(w => w.TenantId == tenantId && w.ApartmentId == apartmentId)
                .Include(w => w.Collection)
                .Include(w => w.Apartment)
                .ToListAsync();
        }

        public async Task<IEnumerable<TenantWishlist>> GetFavoritesOnlyAsync(Guid tenantId, Guid? collectionId = null)
        {
            return await _context.TenantWishlists
                .Where(w => w.TenantId == tenantId && w.IsFavorite)
                .Where(w => !collectionId.HasValue || w.CollectionId == collectionId.Value)
                .Include(w => w.Collection)
                .Include(w => w.Apartment)
                    .ThenInclude(a => a!.Room)
                .Include(w => w.Apartment)
                    .ThenInclude(a => a!.ApartmentMedia)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }
    }
}

