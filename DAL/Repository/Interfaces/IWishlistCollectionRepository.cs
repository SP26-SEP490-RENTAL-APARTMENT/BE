using DAL.Models;

namespace DAL.Repository.Interfaces
{
    public interface IWishlistCollectionRepository : IRepository<WishlistCollection>
    {
        Task<(IEnumerable<WishlistCollection> Items, int TotalCount)> GetTenantCollectionsAsync(
            Guid tenantId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null);

        Task<WishlistCollection?> GetDefaultCollectionAsync(Guid tenantId);

        Task<WishlistCollection?> GetByIdForTenantAsync(Guid tenantId, Guid collectionId);

        Task<bool> ExistsCollectionNameAsync(Guid tenantId, string normalizedName, Guid? excludedCollectionId = null);
    }
}