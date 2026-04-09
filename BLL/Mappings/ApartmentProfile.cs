using AutoMapper;
using Common.DTOs;
using DAL.Models;
using ApartmentBookingStatusEnum = Common.Enums.ApartmentBookingStatus;

namespace BLL.Mappings;

public class ApartmentProfile : Profile
{
    public ApartmentProfile()
    {
        CreateMap<CreateApartmentRequestDto, Apartment>()
            .ForMember(dest => dest.Location, opt => opt.MapFrom(src => new NetTopologySuite.Geometries.Point((double)src.longitude.GetValueOrDefault(), (double)src.latitude.GetValueOrDefault()) { SRID = 4326 }))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => Common.Utils.VietnamTime.Now))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "draft"))
            .ForMember(dest => dest.BookingStatus, opt => opt.MapFrom(src => "available"))
            .ForMember(dest => dest.ApartmentId, opt => opt.Ignore())
            .ForMember(dest => dest.LandlordId, opt => opt.Ignore());

        CreateMap<UpdateApartmentRequestDto, Apartment>()
            .ForMember(dest => dest.Location, opt => opt.Condition(src => src.Latitude.HasValue && src.Longitude.HasValue))
            .ForMember(dest => dest.Location, opt => opt.MapFrom(src => src.Longitude.HasValue && src.Latitude.HasValue 
                ? new NetTopologySuite.Geometries.Point((double)src.Longitude.Value, (double)src.Latitude.Value) { SRID = 4326 } 
                : null))
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Apartment, CreateApartmentResponseDto>()
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.ApartmentMedia.Select(m => m.Url).ToList()));
        CreateMap<CreateApartmentResponseDto, Apartment>(MemberList.Source);

        CreateMap<Apartment, ApartmentResponseDto>()
            .ForMember(dest => dest.LandlordName, opt => opt.MapFrom(src =>
                src.Landlord != null && src.Landlord.LandlordNavigation != null
                    ? src.Landlord.LandlordNavigation.FullName
                    : null))
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.ApartmentMedia.Select(m => m.Url).ToList()));

        CreateMap<Room, RoomResponseDto>();
        CreateMap<CreateRoomRequestDto, Room>()
            .ForMember(dest => dest.RoomId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => Common.Utils.VietnamTime.Now));

        CreateMap<UpdateRoomRequestDto, Room>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Amenity, AmenityResponseDto>();
    }
}
