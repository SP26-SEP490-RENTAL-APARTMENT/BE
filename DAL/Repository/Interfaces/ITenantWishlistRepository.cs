using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface ITenantWishlistRepository : IRepository<TenantWishlist>
    {
        Task<(IEnumerable<TenantWishlist> Items, int TotalCount)> GetTenantWishlistAsync(
            Guid tenantId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            Dictionary<string, string>? filters = null);

        Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId);

        Task<int> GetWishlistCountAsync(Guid tenantId);

        Task<TenantWishlist?> FindByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId);

        Task<IEnumerable<TenantWishlist>> GetFavoritesOnlyAsync(Guid tenantId);
    }
}
