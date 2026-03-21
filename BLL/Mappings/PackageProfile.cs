using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class PackageProfile : Profile
{
    public PackageProfile()
    {
        CreateMap<Package, PackageResponseDto>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.PackagePackages != null ? src.PackagePackages.Select(pp => pp.PackageItem) : null));
        CreateMap<PackageRequestDto, Package>()
            .ForMember(dest => dest.PackageId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => System.DateTime.UtcNow))
            .ForMember(dest => dest.PackagePackages, opt => opt.Ignore());

        CreateMap<PackageItem, PackageItemResponseDto>();
        CreateMap<PackageItemRequestDto, PackageItem>()
            .ForMember(dest => dest.PackageItemId, opt => opt.Ignore());

        // Duplicate mapping removed
    }
}
