using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class CheckTimeRequestProfile : Profile
{
    public CheckTimeRequestProfile()
    {
        CreateMap<CheckTimeRequest, CheckTimeRequestResponseDto>();
        CreateMap<CheckTimeRequestResponseDto, CheckTimeRequest>();
    }
}
