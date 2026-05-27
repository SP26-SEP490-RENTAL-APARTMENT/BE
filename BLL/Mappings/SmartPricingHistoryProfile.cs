using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class SmartPricingHistoryProfile : Profile
{
    public SmartPricingHistoryProfile()
    {
        CreateMap<SmartPricingHistory, SmartPricingResponseDto>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.OccupancyRate, opt => opt.MapFrom(src => src.OccupancyRate ?? 0m))
            .ForMember(dest => dest.Multiplier, opt => opt.MapFrom(src => src.Multiplier ?? 0m))
            .ForMember(dest => dest.Apartment, opt => opt.MapFrom(src => src.Apartment));

        CreateMap<Apartment, SmartPricingApartmentPhotoDto>()
            .ForMember(dest => dest.PhotoUrls, opt => opt.MapFrom(src => src.ApartmentMedia.Select(m => m.Url)));

        CreateMap<SuggestPriceDto, SmartPricingHistory>()
            .ForMember(dest => dest.PricingId, opt => opt.Ignore())
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate));
    }
}
