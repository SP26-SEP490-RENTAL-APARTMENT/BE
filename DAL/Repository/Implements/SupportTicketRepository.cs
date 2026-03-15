using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class SupportTicketRepository : Repository<SupportTicket>, ISupportTicketRepository
    {
        public SupportTicketRepository(AppDbContext context) : base(context)
        {
        }
    }
}