using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class ReportProfile : Profile
{
    public ReportProfile()
    {
        CreateMap<ReportDefinition, ReportDefinitionDto>();
        CreateMap<ReportDefinition, ReportDefinitionResponseDto>()
            .ForMember(dest => dest.DimensionsJson, opt => opt.MapFrom(src => src.QueryConfig != null ? src.QueryConfig.DimensionsJson : null))
            .ForMember(dest => dest.MetricsJson, opt => opt.MapFrom(src => src.QueryConfig != null ? src.QueryConfig.MetricsJson : null))
            .ForMember(dest => dest.FiltersJson, opt => opt.MapFrom(src => src.QueryConfig != null ? src.QueryConfig.FiltersJson : null))
            .ForMember(dest => dest.TimeRangeJson, opt => opt.MapFrom(src => src.QueryConfig != null ? src.QueryConfig.TimeRangeJson : null));
    }
}
