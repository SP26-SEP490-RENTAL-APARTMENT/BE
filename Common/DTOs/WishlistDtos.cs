using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class AddToWishlistRequestDto
{
    [Required(ErrorMessage = "Apartment ID is required")]
    public Guid apartmentId { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? notes { get; set; }
}

public class UpdateWishlistItemRequestDto
{
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? notes { get; set; }

    public bool? isFavorite { get; set; }
}

public class ToggleFavoriteRequestDto
{
    [Required(ErrorMessage = "isFavorite is required")]
    public bool isFavorite { get; set; }
}

public class WishlistItemResponseDto
{
    public Guid wishlistId { get; set; }

    public Guid apartmentId { get; set; }

    public bool isFavorite { get; set; }

    public string? notes { get; set; }

    public DateTime addedAt { get; set; }

    public WishlistApartmentDetailsDto? apartmentDetails { get; set; }
}

public class WishlistApartmentDetailsDto
{
    public Guid apartmentId { get; set; }

    public string? title { get; set; }

    public string? description { get; set; }

    public decimal basePricePerNight { get; set; }

    public string? address { get; set; }

    public string? city { get; set; }

    public string? district { get; set; }

    public sbyte? maxOccupants { get; set; }

    public bool? isPetAllowed { get; set; }

    public decimal? latitude { get; set; }

    public decimal? longitude { get; set; }

    public string? status { get; set; }

    public DateTime? createdAt { get; set; }
}

public class WishlistResponseDto
{
    public List<WishlistItemResponseDto> items { get; set; } = new();

    public int totalCount { get; set; }

    public int page { get; set; }

    public int pageSize { get; set; }
}
