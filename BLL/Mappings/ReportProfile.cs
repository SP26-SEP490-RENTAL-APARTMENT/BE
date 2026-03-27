using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class ReportProfile : Profile
{
    public ReportProfile()
    {
        CreateMap<ReportDefinition, ReportDefinitionDto>();
    }
}
