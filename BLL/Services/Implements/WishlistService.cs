using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class WishlistService : BaseService<TenantWishlist>, IWishlistService
{
    private readonly ITenantWishlistRepository _wishlistRepository;
    private readonly IWishlistCollectionRepository _wishlistCollectionRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IMapper _mapper;
    private const int MaxWishlistItems = 100;
    private const int MaxCollections = 20;
    private const string DefaultCollectionName = "General";

    public WishlistService(
        ITenantWishlistRepository wishlistRepository,
        IWishlistCollectionRepository wishlistCollectionRepository,
        IApartmentRepository apartmentRepository,
        IMapper mapper)
        : base(wishlistRepository)
    {
        _wishlistRepository = wishlistRepository;
        _wishlistCollectionRepository = wishlistCollectionRepository;
        _apartmentRepository = apartmentRepository;
        _mapper = mapper;
    }

    public async Task<(IEnumerable<WishlistItemResponseDto> Items, int TotalCount)> GetTenantWishlistAsync(
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
        if (collectionId.HasValue)
        {
            await EnsureCollectionOwnedByTenantAsync(tenantId, collectionId.Value);
        }

        var (items, totalCount) = await _wishlistRepository.GetTenantWishlistAsync(
            tenantId, page, pageSize, sortBy, sortOrder, search, priceMin, priceMax, collectionId, filters);

        var dtos = items.Select(item => _mapper.Map<WishlistItemResponseDto>(item)).ToList();
        return (dtos, totalCount);
    }

    public async Task<WishlistItemResponseDto> AddToWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null, string? notes = null)
    {
        // Validate apartment exists
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
        {
            throw new ArgumentException($"Apartment with ID {apartmentId} not found.");
        }

        var resolvedCollection = await ResolveCollectionAsync(tenantId, collectionId);

        // Check if already in wishlist
        var exists = await _wishlistRepository.IsApartmentInWishlistAsync(tenantId, apartmentId, resolvedCollection.CollectionId);
        if (exists)
        {
            throw new InvalidOperationException($"Apartment is already in this collection.");
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
            CollectionId = resolvedCollection.CollectionId,
            Notes = notes,
            IsFavorite = false,
            CreatedAt = Common.Utils.VietnamTime.Now
        };

        await _wishlistRepository.AddAsync(wishlistItem);
        await _wishlistRepository.SaveChangesAsync();

        // Reload with apartment details
        var reloaded = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId, resolvedCollection.CollectionId)
            ?? throw new InvalidOperationException("Failed to retrieve newly created wishlist item.");

        return _mapper.Map<WishlistItemResponseDto>(reloaded);
    }

    public async Task RemoveFromWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null)
    {
        var resolvedCollection = await ResolveCollectionAsync(tenantId, collectionId);

        var wishlistItem = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId, resolvedCollection.CollectionId);
        if (wishlistItem == null)
        {
            throw new ArgumentException($"Apartment is not in the specified collection.");
        }

        _wishlistRepository.Remove(wishlistItem);
        await _wishlistRepository.SaveChangesAsync();
    }

    public async Task<WishlistItemResponseDto> ToggleFavoriteAsync(Guid tenantId, Guid apartmentId, bool isFavorite, Guid? collectionId = null)
    {
        var resolvedCollection = await ResolveCollectionAsync(tenantId, collectionId);

        var wishlistItem = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId, resolvedCollection.CollectionId);
        if (wishlistItem == null)
        {
            throw new ArgumentException($"Apartment is not in the specified collection.");
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

    public async Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null)
    {
        var resolvedCollection = await ResolveCollectionAsync(tenantId, collectionId);
        return await _wishlistRepository.IsApartmentInWishlistAsync(tenantId, apartmentId, resolvedCollection.CollectionId);
    }

    public async Task<WishlistCollectionResponseDto> CreateCollectionAsync(Guid tenantId, string name, string? description = null)
    {
        await EnsureDefaultCollectionAsync(tenantId);

        var trimmedName = NormalizeName(name);
        description = TrimDescription(description);

        var (_, existingCount) = await _wishlistCollectionRepository.GetTenantCollectionsAsync(
            tenantId,
            1,
            MaxCollections + 1,
            null,
            null,
            null,
            null);

        if (existingCount >= MaxCollections)
        {
            throw new InvalidOperationException($"Maximum {MaxCollections} collections allowed.");
        }

        var hasDuplicateName = await _wishlistCollectionRepository.ExistsCollectionNameAsync(tenantId, trimmedName.ToLower());
        if (hasDuplicateName)
        {
            throw new InvalidOperationException("A collection with the same name already exists.");
        }

        var collection = new WishlistCollection
        {
            CollectionId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = trimmedName,
            Description = description,
            IsDefault = false,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };

        await _wishlistCollectionRepository.AddAsync(collection);
        await _wishlistCollectionRepository.SaveChangesAsync();

        return _mapper.Map<WishlistCollectionResponseDto>(collection);
    }

    public async Task<(IEnumerable<WishlistCollectionResponseDto> Items, int TotalCount)> GetCollectionsAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null)
    {
        await EnsureDefaultCollectionAsync(tenantId);

        var (collections, totalCount) = await _wishlistCollectionRepository.GetTenantCollectionsAsync(
            tenantId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters);

        var mapped = collections.Select(c => _mapper.Map<WishlistCollectionResponseDto>(c)).ToList();
        return (mapped, totalCount);
    }

    public async Task<WishlistCollectionResponseDto> UpdateCollectionAsync(Guid tenantId, Guid collectionId, string name, string? description = null)
    {
        var collection = await EnsureCollectionOwnedByTenantAsync(tenantId, collectionId);

        var trimmedName = NormalizeName(name);
        description = TrimDescription(description);

        var hasDuplicateName = await _wishlistCollectionRepository.ExistsCollectionNameAsync(tenantId, trimmedName.ToLower(), collectionId);
        if (hasDuplicateName)
        {
            throw new InvalidOperationException("A collection with the same name already exists.");
        }

        collection.Name = trimmedName;
        collection.Description = description;
        collection.UpdatedAt = Common.Utils.VietnamTime.Now;

        _wishlistCollectionRepository.Update(collection);
        await _wishlistCollectionRepository.SaveChangesAsync();

        return _mapper.Map<WishlistCollectionResponseDto>(collection);
    }

    public async Task DeleteCollectionAsync(Guid tenantId, Guid collectionId)
    {
        var collection = await EnsureCollectionOwnedByTenantAsync(tenantId, collectionId);
        if (collection.IsDefault)
        {
            throw new InvalidOperationException("Default collection cannot be deleted.");
        }

        _wishlistCollectionRepository.Remove(collection);
        await _wishlistCollectionRepository.SaveChangesAsync();
    }

    public async Task<WishlistItemResponseDto> MoveWishlistItemAsync(Guid tenantId, Guid apartmentId, Guid sourceCollectionId, Guid targetCollectionId)
    {
        if (sourceCollectionId == targetCollectionId)
        {
            throw new ArgumentException("Source and target collections must be different.");
        }

        await EnsureCollectionOwnedByTenantAsync(tenantId, sourceCollectionId);
        var targetCollection = await EnsureCollectionOwnedByTenantAsync(tenantId, targetCollectionId);

        var sourceItem = await _wishlistRepository.FindByTenantAndApartmentAsync(tenantId, apartmentId, sourceCollectionId);
        if (sourceItem == null)
        {
            throw new ArgumentException("Apartment is not in the source collection.");
        }

        var alreadyInTarget = await _wishlistRepository.IsApartmentInWishlistAsync(tenantId, apartmentId, targetCollectionId);
        if (alreadyInTarget)
        {
            throw new InvalidOperationException("Apartment already exists in target collection.");
        }

        sourceItem.CollectionId = targetCollectionId;
        sourceItem.Collection = targetCollection;
        _wishlistRepository.Update(sourceItem);
        await _wishlistRepository.SaveChangesAsync();

        return _mapper.Map<WishlistItemResponseDto>(sourceItem);
    }

    private async Task<WishlistCollection> ResolveCollectionAsync(Guid tenantId, Guid? collectionId)
    {
        if (collectionId.HasValue)
        {
            return await EnsureCollectionOwnedByTenantAsync(tenantId, collectionId.Value);
        }

        return await EnsureDefaultCollectionAsync(tenantId);
    }

    private async Task<WishlistCollection> EnsureCollectionOwnedByTenantAsync(Guid tenantId, Guid collectionId)
    {
        var collection = await _wishlistCollectionRepository.GetByIdForTenantAsync(tenantId, collectionId);
        if (collection == null)
        {
            throw new ArgumentException("Wishlist collection not found.");
        }

        return collection;
    }

    private async Task<WishlistCollection> EnsureDefaultCollectionAsync(Guid tenantId)
    {
        var collection = await _wishlistCollectionRepository.GetDefaultCollectionAsync(tenantId);
        if (collection != null)
        {
            return collection;
        }

        collection = new WishlistCollection
        {
            CollectionId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = DefaultCollectionName,
            Description = "Default wishlist collection",
            IsDefault = true,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };

        await _wishlistCollectionRepository.AddAsync(collection);
        await _wishlistCollectionRepository.SaveChangesAsync();
        return collection;
    }

    private static string NormalizeName(string name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Collection name is required.");
        }

        return trimmedName.Length > 100 ? trimmedName[..100] : trimmedName;
    }

    private static string? TrimDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }
}
