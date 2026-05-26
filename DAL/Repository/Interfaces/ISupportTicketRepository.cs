using System.Linq.Expressions;
using DAL.Models;

namespace DAL.Repository.Interfaces
{
    public interface ISupportTicketRepository : IRepository<SupportTicket>
    {
        public Task<IEnumerable<SupportTicket >> GetAllWithAttatchmentAsync(Guid ticketId);
        public Task<IEnumerable<SupportTicket>> GetAllWithAttatchmentByUserIdAsync(Guid userId);
        Task<IEnumerable<SupportTicket>> FindWithAttachmentsNoTrackingAsync(Expression<Func<SupportTicket, bool>> predicate);
    }
}