using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserDto>().ReverseMap();
            CreateMap<CreateUserDto, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => src.Password)); // Using MapFrom since we usually would hash it, assume service hashes later or modify here
            CreateMap<UpdateUserDto, User>();
        }
    }
}