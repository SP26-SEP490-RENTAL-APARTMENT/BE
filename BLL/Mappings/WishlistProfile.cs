using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class WishlistProfile : Profile
{
    public WishlistProfile()
    {
        // Map TenantWishlist to WishlistItemResponseDto
        CreateMap<TenantWishlist, WishlistItemResponseDto>()
            .ForMember(dest => dest.wishlistId, opt => opt.MapFrom(src => src.WishlistId))
            .ForMember(dest => dest.apartmentId, opt => opt.MapFrom(src => src.ApartmentId))
            .ForMember(dest => dest.collectionId, opt => opt.MapFrom(src => src.CollectionId))
            .ForMember(dest => dest.collectionName, opt => opt.MapFrom(src => src.Collection.Name))
            .ForMember(dest => dest.isFavorite, opt => opt.MapFrom(src => src.IsFavorite))
            .ForMember(dest => dest.notes, opt => opt.MapFrom(src => src.Notes))
            .ForMember(dest => dest.addedAt, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.apartmentDetails, opt =>
                opt.MapFrom(src => src.Apartment != null
                    ? new WishlistApartmentDetailsDto
                    {
                        apartmentId = src.Apartment.ApartmentId,
                        title = src.Apartment.Title,
                        description = src.Apartment.Description,
                        basePricePerNight = src.Apartment.BasePricePerNight,
                        address = src.Apartment.Address,
                        city = src.Apartment.City,
                        district = src.Apartment.District,
                        maxOccupants = src.Apartment.MaxOccupants,
                        maxInfants = src.Apartment.MaxInfants,
                        maxPets = src.Apartment.MaxPets,
                        isPetAllowed = src.Apartment.IsPetAllowed,
                        latitude = src.Apartment.Latitude,
                        longitude = src.Apartment.Longitude,
                        status = src.Apartment.Status,
                        createdAt = src.Apartment.CreatedAt
                    }
                    : null));

        CreateMap<AddToWishlistRequestDto, TenantWishlist>()
            .ForMember(dest => dest.WishlistId, opt => opt.Ignore())
            .ForMember(dest => dest.ApartmentId, opt => opt.MapFrom(src => src.apartmentId))
            .ForMember(dest => dest.CollectionId, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.notes))
            .ForMember(dest => dest.IsFavorite, opt => opt.MapFrom(_ => false))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => Common.Utils.VietnamTime.Now))
            .ForMember(dest => dest.Tenant, opt => opt.Ignore())
            .ForMember(dest => dest.Apartment, opt => opt.Ignore())
            .ForMember(dest => dest.Collection, opt => opt.Ignore());

        CreateMap<WishlistCollection, WishlistCollectionResponseDto>()
            .ForMember(dest => dest.collectionId, opt => opt.MapFrom(src => src.CollectionId))
            .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.isDefault, opt => opt.MapFrom(src => src.IsDefault))
            .ForMember(dest => dest.createdAt, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.updatedAt, opt => opt.MapFrom(src => src.UpdatedAt));
    }
}
