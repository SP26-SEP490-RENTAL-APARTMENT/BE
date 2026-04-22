using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class PropertyInspectionProfile : Profile
{
    public PropertyInspectionProfile()
    {
        CreateMap<InspectionPhoto, InspectionPhotoResponseDto>();
        CreateMap<PropertyInspection, PropertyInspectionResponseDto>()
            .ForMember(dest => dest.ApartmentName, opt => opt.MapFrom(src => src.Apartment != null ? src.Apartment.Title : null))
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.InspectionPhotos));
        CreateMap<PropertyInspectionRequestDto, PropertyInspection>()
            .ForMember(dest => dest.InspectionId, opt => opt.Ignore())
            .ForMember(dest => dest.ScheduledDate, opt => opt.MapFrom(src =>
                src.ScheduledDateTime.HasValue
                    ? (DateOnly?)DateOnly.FromDateTime(src.ScheduledDateTime.Value)
                    : null));
        CreateMap<CreatePropertyInspectionDto, PropertyInspection>()
            .ForMember(dest => dest.InspectionId, opt => opt.Ignore())
            .ForMember(dest => dest.ScheduledDate, opt => opt.MapFrom(src =>
                src.ScheduledDateTime.HasValue
                    ? (DateOnly?)DateOnly.FromDateTime(src.ScheduledDateTime.Value)
                    : null));
    }
}