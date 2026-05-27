using System.Globalization;
using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class PaymentProfile : Profile
{
    public PaymentProfile()
    {
        var viCulture = CultureInfo.GetCultureInfo("vi-VN");

        CreateMap<Payment, PaymentHistoryDto>()
            .ForMember(
                dest => dest.SignedAmountDisplay,
                opt => opt.MapFrom(src =>
                    (string.Equals(src.PaymentType, "refund", StringComparison.OrdinalIgnoreCase) ? "-" : "+")
                    + src.Amount.ToString("N0", viCulture)
                )
            );
    }
}
