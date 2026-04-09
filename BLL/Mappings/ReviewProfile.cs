using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Review, ReviewResponseDto>();
        CreateMap<CreateReviewRequestDto, Review>()
            .ForMember(dest => dest.ReviewId, opt => opt.Ignore())
            .ForMember(dest => dest.ReviewerId, opt => opt.Ignore()) // usually resolved from token
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => Common.Utils.VietnamTime.Now));

        CreateMap<UpdateReviewRequestDto, Review>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
