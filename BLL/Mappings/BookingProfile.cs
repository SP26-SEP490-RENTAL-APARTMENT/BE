using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class BookingProfile : Profile
{
    public BookingProfile()
    {
        CreateMap<Booking, BookingResponseDto>()
            .ForMember(dest => dest.TenantFullName,
                opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.TenantNavigation.FullName : null))
            .ForMember(dest => dest.ActualCheckIn,
                opt => opt.MapFrom(src => src.BookingCheckTime != null ? src.BookingCheckTime.ActualCheckIn : null))
            .ForMember(dest => dest.ActualCheckOut,
                opt => opt.MapFrom(src => src.BookingCheckTime != null ? src.BookingCheckTime.ActualCheckOut : null))
            .ForMember(dest => dest.RemainingBalance,
                opt => opt.MapFrom(src => src.RemainingAmount))
            .ForMember(dest => dest.Images,
                opt => opt.MapFrom(src => src.Apartment != null && src.Apartment.ApartmentMedia != null
                    ? src.Apartment.ApartmentMedia
                        .Where(media => !string.IsNullOrWhiteSpace(media.Url))
                        .Select(media => media.Url)
                        .ToList()
                    : new List<string>()));
        CreateMap<CreateBookingRequestDto, Booking>()
            .ForMember(dest => dest.BookingId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => Common.Utils.VietnamTime.Now))
            .ForMember(dest => dest.PaymentMode, opt => opt.MapFrom(src => src.PaymentMode.ToString()));
            
        CreateMap<UpdateBookingRequestDto, Booking>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
