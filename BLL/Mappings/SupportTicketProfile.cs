using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings
{
    public class SupportTicketProfile : Profile
    {
        public SupportTicketProfile()
        {
            CreateMap<SupportTicket, SupportTicketDto>().ReverseMap();
            CreateMap<CreateSupportTicketDto, SupportTicket>();
            CreateMap<UpdateSupportTicketDto, SupportTicket>();
        }
    }
}