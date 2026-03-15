using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class ApartmentProfile : Profile
{
    public ApartmentProfile()
    {
        CreateMap<CreateApartmentRequestDto, Apartment>()
            .ForMember(dest => dest.Location, opt => opt.MapFrom(src => new NetTopologySuite.Geometries.Point((double)src.Longitude.GetValueOrDefault(), (double)src.Latitude.GetValueOrDefault()) { SRID = 4326 }))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => System.DateTime.UtcNow))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "draft"))
            .ForMember(dest => dest.ApartmentId, opt => opt.Ignore())
            .ForMember(dest => dest.LandlordId, opt => opt.Ignore());

        CreateMap<Apartment, CreateApartmentResponseDto>()
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.ApartmentMedia.Select(m => m.Url).ToList()));
        CreateMap<CreateApartmentResponseDto, Apartment>(MemberList.Source);

        CreateMap<Apartment, ApartmentResponseDto>()
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.ApartmentMedia.Select(m => m.Url).ToList()));

        CreateMap<Room, RoomResponseDto>();
        CreateMap<Amenity, AmenityResponseDto>();
    }
}
