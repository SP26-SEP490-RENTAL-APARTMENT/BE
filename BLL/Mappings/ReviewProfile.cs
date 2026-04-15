using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Review, ReviewResponseDto>()
            .ForMember(dest => dest.TeanantId, opt => opt.MapFrom(src => src.ReviewerId))
            .ForMember(dest => dest.TenantName, opt => opt.MapFrom(src => src.Reviewer.FullName))
            .ForMember(dest => dest.LandlordId, opt => opt.MapFrom(src => src.ReviewedId))
            .ForMember(dest => dest.LandlordName, opt => opt.MapFrom(src => src.Reviewed.FullName))
            .ForMember(dest => dest.Comment, opt => opt.MapFrom(src => src.CommentEn ?? src.CommentVi));

        CreateMap<CreateReviewRequestDto, Review>()
            .ForMember(dest => dest.ReviewId, opt => opt.Ignore())
            .ForMember(dest => dest.ReviewerId, opt => opt.Ignore()) // usually resolved from token
            .ForMember(dest => dest.ReviewedId, opt => opt.Ignore())
            .ForMember(dest => dest.ApartmentId, opt => opt.Ignore())
            .ForMember(dest => dest.CommentEn, opt => opt.MapFrom(src => src.Comment))
            .ForMember(dest => dest.CommentVi, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => Common.Utils.VietnamTime.Now));

        CreateMap<UpdateReviewRequestDto, Review>()
            .ForMember(dest => dest.CommentEn, opt => opt.MapFrom(src => src.Comment))
            .ForMember(dest => dest.CommentVi, opt => opt.Ignore())
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
