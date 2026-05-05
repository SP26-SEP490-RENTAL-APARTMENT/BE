using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces
{
    public interface ISupportTicketService : IBaseService<SupportTicket>
    {
        Task<SupportTicket> CreateTicketAsync(SupportTicket ticket);
        Task<SupportTicket> UpdateTicketByStaffAsync(Guid ticketId, UpdateSupportTicketDto ticketDto, Guid actorUserId);
        Task<SupportTicket> CreateFollowUpTicketAsync(Guid originalTicketId, Guid requesterUserId, string details);
        Task<SupportTicket> ResolveTicketByStaffAsync(Guid ticketId, string resolutionNotes, Guid staffUserId);
        Task<SupportTicket> UpdateTicketByCreatorStatusAsync(Guid ticketId, Guid requesterUserId, UserUpdateStatusRequestDto updateDto);
        Task<IEnumerable<SupportTicketAttachment>> UploadTicketAttachmentsAsync(Guid ticketId, UploadSupportTicketAttachmentDto dto, Guid uploadedByUserId);
    }
}