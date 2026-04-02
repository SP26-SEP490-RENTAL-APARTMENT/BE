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
            string? search = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            Guid? collectionId = null,
            Dictionary<string, string>? filters = null);

        Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid collectionId);

        Task<int> GetWishlistCountAsync(Guid tenantId);

        Task<TenantWishlist?> FindByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId, Guid collectionId);

        Task<IEnumerable<TenantWishlist>> FindAllByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId);

        Task<IEnumerable<TenantWishlist>> GetFavoritesOnlyAsync(Guid tenantId, Guid? collectionId = null);
    }
}
