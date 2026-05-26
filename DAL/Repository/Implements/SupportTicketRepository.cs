using System.Linq.Expressions;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class SupportTicketRepository : Repository<SupportTicket>, ISupportTicketRepository
    {
        public SupportTicketRepository(AppDbContext context) : base(context)
        {
        }

        public override async Task<SupportTicket?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(s => s.SupportTicketAttachments)
                .Include(s => s.SupportTicketAssignments)
                .FirstOrDefaultAsync(s => s.TicketId == id);
        }

        public override async Task<(IEnumerable<SupportTicket> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _dbSet
                .Include(s => s.SupportTicketAttachments)
                .Include(s => s.SupportTicketAssignments)
                .AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<IEnumerable<SupportTicket>> GetAllWithAttatchmentAsync(Guid ticketId)
        {
            return await _dbSet
                .Include(s => s.SupportTicketAttachments)
                .Include(s => s.SupportTicketAssignments)
                .Where(s => s.TicketId == ticketId)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupportTicket>> GetAllWithAttatchmentByUserIdAsync(Guid userId)
        {
            return await _dbSet
                .Include(s => s.SupportTicketAttachments)
                .Include(s => s.SupportTicketAssignments)
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupportTicket>> FindWithAttachmentsNoTrackingAsync(Expression<Func<SupportTicket, bool>> predicate)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(s => s.SupportTicketAttachments)
                .Include(s => s.SupportTicketAssignments)
                .Where(predicate)
                .ToListAsync();
        }
    }
}