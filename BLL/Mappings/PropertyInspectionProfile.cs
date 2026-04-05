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
            .ForMember(dest => dest.Photos, opt => opt.MapFrom(src => src.InspectionPhotos));
        CreateMap<PropertyInspectionRequestDto, PropertyInspection>()
            .ForMember(dest => dest.InspectionId, opt => opt.Ignore());
        CreateMap<CreatePropertyInspectionDto, PropertyInspection>()
            .ForMember(dest => dest.InspectionId, opt => opt.Ignore());
    }
}