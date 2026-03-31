using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class WishlistService : BaseService<TenantWishlist>, IWishlistService
{
    private readonly ITenantWishlistRepository _wishlistRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IMapper _mapper;
    private const int MaxWishlistItems = 100;

    public WishlistService(
        ITenantWishlistRepository wishlistRepository,
        IApartmentRepository apartmentRepository,
        IMapper mapper)
        : base(wishlistRepository)
    {
        _wishlistRepository = wishlistRepository;
        _apartmentRepository = apartmentRepository;
        _mapper = mapper;
    }

    public async Task<(IEnumerable<WishlistItemResponseDto> Items, int TotalCount)> GetTenantWishlistAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        decimal? priceMin = null,
        decimal? priceMax = null,
        Dictionary<string, string>? filters = null)
    {
        var (items, totalCount) = await _wishlistRepository.GetTenantWishlistAsync(
            tenantId, page, pageSize, sortBy, sortOrder, priceMin, priceMax, filters);

        var dtos = items.Select(item => _mapper.Map<WishlistItemResponseDto>(item)).ToList();
        return (dtos, totalCount);
    }

    public async Task<WishlistItemResponseDto> AddToWishlistAsync(Guid tenantId, Guid apartmentId, string? notes = null)
    {
        // Validate apartment exists
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
        {
            throw new ArgumentException($"Apartment with ID {apartmentId} not found.");
        }

        // Check if already in wishlist
        var exists = await _wishlistRepository.IsApartmentInWishlistAsync(tenantId, apartmentId);
        if (exists)
        {
            throw new InvalidOperationException($"Apartment is already in your wishlist.");
        }

        // Check wishlist size limit
        var count = await _wishlistRepository.GetWishlistCountAsync(tenantId);
        if (count >= MaxWishlistItems)
        {
            throw new InvalidOperationException($"Wishlist is full. Maximum {MaxWishlistItems} items allowed.");
        }

        // Trim notes if needed
        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 500)
        {
            notes = notes.Substring(0, 500);
        }

        // Create wishlist item
        var wishlistItem = new TenantWishlist
        {
            WishlistId = Guid.NewGuid(),
            TenantId = tenantId,
            ApartmentId = apartmentId,
            Notes = notes,
            IsFavorite = false,
            CreatedAt = DateTime.UtcNow
        };

        await _wishlistRepository.AddAsync(wishlistItem);
        await _wishlistRepository.SaveChangesAsync();

        // Reload with apartment details
        var reloaded = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId)
            ?? throw new InvalidOperationException("Failed to retrieve newly created wishlist item.");

        return _mapper.Map<WishlistItemResponseDto>(reloaded);
    }

    public async Task RemoveFromWishlistAsync(Guid tenantId, Guid apartmentId)
    {
        var wishlistItem = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId);
        if (wishlistItem == null)
        {
            throw new ArgumentException($"Apartment is not in your wishlist.");
        }

        _wishlistRepository.Remove(wishlistItem);
        await _wishlistRepository.SaveChangesAsync();
    }

    public async Task<WishlistItemResponseDto> ToggleFavoriteAsync(Guid tenantId, Guid apartmentId, bool isFavorite)
    {
        var wishlistItem = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId);
        if (wishlistItem == null)
        {
            throw new ArgumentException($"Apartment is not in your wishlist.");
        }

        wishlistItem.IsFavorite = isFavorite;
        _wishlistRepository.Update(wishlistItem);
        await _wishlistRepository.SaveChangesAsync();

        return _mapper.Map<WishlistItemResponseDto>(wishlistItem);
    }

    public async Task<int> GetWishlistCountAsync(Guid tenantId)
    {
        return await _wishlistRepository.GetWishlistCountAsync(tenantId);
    }

    public async Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId)
    {
        return await _wishlistRepository.IsApartmentInWishlistAsync(tenantId, apartmentId);
    }
}
