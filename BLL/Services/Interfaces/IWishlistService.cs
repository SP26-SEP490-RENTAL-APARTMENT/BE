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
        decimal? priceMin = null,
        decimal? priceMax = null,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Adds an apartment to the tenant's wishlist.
    /// Validates: apartment exists, not already wishlisted, wishlist size < 100 items.
    /// </summary>
    Task<WishlistItemResponseDto> AddToWishlistAsync(Guid tenantId, Guid apartmentId, string? notes = null);

    /// <summary>
    /// Removes an apartment from the tenant's wishlist.
    /// </summary>
    Task RemoveFromWishlistAsync(Guid tenantId, Guid apartmentId);

    /// <summary>
    /// Toggles the favorite flag for a wishlisted apartment.
    /// </summary>
    Task<WishlistItemResponseDto> ToggleFavoriteAsync(Guid tenantId, Guid apartmentId, bool isFavorite);

    /// <summary>
    /// Gets the count of items in a tenant's wishlist (for limit validation).
    /// </summary>
    Task<int> GetWishlistCountAsync(Guid tenantId);

    /// <summary>
    /// Checks if an apartment is already in the tenant's wishlist.
    /// </summary>
    Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId);
}
