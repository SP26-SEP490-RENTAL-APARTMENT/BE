using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces
{
    public interface ISupportTicketService : IBaseService<SupportTicket>
    {
        Task<SupportTicket> CreateTicketAsync(SupportTicket ticket);
        Task<SupportTicket> UpdateTicketByStaffAsync(Guid ticketId, UpdateSupportTicketDto ticketDto, Guid actorUserId);
        Task<SupportTicket> CreateFollowUpTicketAsync(Guid originalTicketId, Guid requesterUserId, string details);
    }
}