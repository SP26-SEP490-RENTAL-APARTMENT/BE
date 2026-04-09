using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings;

public class RoomProfile : Profile
{
    public RoomProfile()
    {
        CreateMap<Room, RoomResponseDto>();
        CreateMap<CreateRoomRequestDto, Room>()
            .ForMember(dest => dest.RoomId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => Common.Utils.VietnamTime.Now));

        CreateMap<UpdateRoomRequestDto, Room>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
