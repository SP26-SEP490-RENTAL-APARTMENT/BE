using AutoMapper;
using Common.DTOs;
using DAL.Models;

namespace BLL.Mappings
{
    public class NotificationProfile : Profile
    {
        public NotificationProfile()
        {
            CreateMap<Notification, NotificationDto>();
        }
    }
}