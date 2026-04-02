using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IWishlistService : IBaseService<TenantWishlist>
{
    /// <summary>
    /// Retrieves the tenant's wishlisted apartments with pagination, sorting, and filtering.
    /// </summary>
    Task<(IEnumerable<WishlistItemResponseDto> Items, int TotalCount)> GetTenantWishlistAsync(
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

    /// <summary>
    /// Adds an apartment to the tenant's wishlist.
    /// Validates: apartment exists, not already wishlisted, wishlist size < 100 items.
    /// </summary>
    Task<WishlistItemResponseDto> AddToWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null, string? notes = null);

    /// <summary>
    /// Removes an apartment from the tenant's wishlist.
    /// </summary>
    Task RemoveFromWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null);

    /// <summary>
    /// Toggles the favorite flag for a wishlisted apartment.
    /// </summary>
    Task<WishlistItemResponseDto> ToggleFavoriteAsync(Guid tenantId, Guid apartmentId, bool isFavorite, Guid? collectionId = null);

    /// <summary>
    /// Gets the count of items in a tenant's wishlist (for limit validation).
    /// </summary>
    Task<int> GetWishlistCountAsync(Guid tenantId);

    /// <summary>
    /// Checks if an apartment is already in the tenant's wishlist.
    /// </summary>
    Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null);

    /// <summary>
    /// Creates a collection for tenant wishlist organization.
    /// </summary>
    Task<WishlistCollectionResponseDto> CreateCollectionAsync(Guid tenantId, string name, string? description = null);

    /// <summary>
    /// Lists all wishlist collections for a tenant.
    /// </summary>
    Task<(IEnumerable<WishlistCollectionResponseDto> Items, int TotalCount)> GetCollectionsAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Updates a tenant wishlist collection.
    /// </summary>
    Task<WishlistCollectionResponseDto> UpdateCollectionAsync(Guid tenantId, Guid collectionId, string name, string? description = null);

    /// <summary>
    /// Deletes a tenant wishlist collection and all wishlist items in that collection.
    /// </summary>
    Task DeleteCollectionAsync(Guid tenantId, Guid collectionId);

    /// <summary>
    /// Moves an apartment from one wishlist collection to another for a tenant.
    /// </summary>
    Task<WishlistItemResponseDto> MoveWishlistItemAsync(Guid tenantId, Guid apartmentId, Guid sourceCollectionId, Guid targetCollectionId);
}
