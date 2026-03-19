using AutoMapper;
using Common.DTOs;
using DAL.Models;
using NetTopologySuite.Geometries;

namespace BLL.Mappings;

public class NearbyAttractionProfile : Profile
{
    public NearbyAttractionProfile()
    {
        CreateMap<NearbyAttraction, NearbyAttractionResponseDto>()
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Location != null ? src.Location.Y : 0))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Location != null ? src.Location.X : 0));

        CreateMap<CreateNearbyAttractionRequestDto, NearbyAttraction>()
            .ForMember(dest => dest.AttractionId, opt => opt.Ignore())
            .ForMember(dest => dest.Location, opt => opt.MapFrom(src => new Point(src.Longitude, src.Latitude) { SRID = 4326 }));

        CreateMap<UpdateNearbyAttractionRequestDto, NearbyAttraction>()
            .ForMember(dest => dest.Location, opt => opt.Condition(src => src.Latitude.HasValue && src.Longitude.HasValue))
            .ForMember(dest => dest.Location, opt => opt.MapFrom(src => src.Latitude.HasValue && src.Longitude.HasValue 
                ? new Point(src.Longitude.Value, src.Latitude.Value) { SRID = 4326 } 
                : null))
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
