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
            .ForMember(dest => dest.ScheduledDateTime, opt => opt.MapFrom(src => src.ScheduledDate))
            .ForMember(dest => dest.CompletedDateTime, opt => opt.MapFrom(src =>
                src.CompletedDate.HasValue
                    ? src.CompletedDate.Value.ToDateTime(TimeOnly.MinValue)
                    : (DateTime?)null))
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.InspectionPhotos));
        CreateMap<PropertyInspectionRequestDto, PropertyInspection>()
            .ForMember(dest => dest.InspectionId, opt => opt.Ignore())
            .ForMember(dest => dest.ScheduledDate, opt => opt.MapFrom(src => src.ScheduledDateTime));
        CreateMap<CreatePropertyInspectionDto, PropertyInspection>()
            .ForMember(dest => dest.InspectionId, opt => opt.Ignore())
            .ForMember(dest => dest.ScheduledDate, opt => opt.MapFrom(src => src.ScheduledDateTime));
    }
}