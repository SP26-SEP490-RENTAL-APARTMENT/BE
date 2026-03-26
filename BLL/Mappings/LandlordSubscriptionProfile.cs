using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class LandlordSubscriptionProfile : Profile
{
    public LandlordSubscriptionProfile()
    {
        CreateMap<LandlordSubscription, LandlordSubscriptionHistoryDto>();
    }
}
