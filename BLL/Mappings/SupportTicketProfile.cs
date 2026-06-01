using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings
{
    public class SupportTicketProfile : Profile
    {
        public SupportTicketProfile()
        {
            CreateMap<SupportTicket, SupportTicketDto>()
                .ForMember(dest => dest.Attachments, opt => opt.MapFrom(src => src.SupportTicketAttachments))
                .ReverseMap();
            CreateMap<CreateSupportTicketDto, SupportTicket>();
            CreateMap<UpdateSupportTicketDto, SupportTicket>();
            CreateMap<SupportTicketAttachment, SupportTicketAttachmentDto>()
                .ReverseMap();
        }
    }
}