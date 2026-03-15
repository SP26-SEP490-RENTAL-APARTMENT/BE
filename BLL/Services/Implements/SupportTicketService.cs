using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class SupportTicketService : BaseService<SupportTicket>, ISupportTicketService
    {
        public SupportTicketService(ISupportTicketRepository repository) : base(repository)
        {
        }
    }
}