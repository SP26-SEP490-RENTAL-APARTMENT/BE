using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class PackageProfile : Profile
{
    public PackageProfile()
    {
        CreateMap<Package, PackageResponseDto>();
        CreateMap<PackageRequestDto, Package>()
            .ForMember(dest => dest.PackageId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => System.DateTime.UtcNow));

        CreateMap<PackageItem, PackageItemResponseDto>();
        CreateMap<PackageItemRequestDto, PackageItem>()
            .ForMember(dest => dest.PackageItemId, opt => opt.Ignore());
    }
}
