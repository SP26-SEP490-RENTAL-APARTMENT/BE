using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class SmartPricingHistoryProfile : Profile
{
    public SmartPricingHistoryProfile()
    {
        CreateMap<SmartPricingHistory, SmartPricingResponseDto>();
        CreateMap<SuggestPriceDto, SmartPricingHistory>()
            .ForMember(dest => dest.PricingId, opt => opt.Ignore());
    }
}
