using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Tests
{
    internal class FakeSupportTicketRepository : ISupportTicketRepository
    {
        private readonly InMemoryRepository<SupportTicket> _repo;

        public FakeSupportTicketRepository(InMemoryRepository<SupportTicket> repo)
        {
            _repo = repo;
        }

        public Task AddAsync(SupportTicket entity) => _repo.AddAsync(entity);
        public Task<IEnumerable<SupportTicket>> FindAsync(Expression<Func<SupportTicket, bool>> predicate) => _repo.FindAsync(predicate);
        public Task<IEnumerable<SupportTicket>> FindNoTrackingAsync(Expression<Func<SupportTicket, bool>> predicate) => _repo.FindNoTrackingAsync(predicate);
        public Task<SupportTicket?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
        public Task<(IEnumerable<SupportTicket> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => _repo.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, allowedColumns);
        public void Remove(SupportTicket entity) => _repo.Remove(entity);
        public void Update(SupportTicket entity) => _repo.Update(entity);
        public Task<int> SaveChangesAsync() => _repo.SaveChangesAsync();

        public Task<IEnumerable<SupportTicket>> GetAllWithAttatchmentAsync(Guid ticketId) => Task.FromResult<IEnumerable<SupportTicket>>(Array.Empty<SupportTicket>());
        public Task<IEnumerable<SupportTicket>> GetAllWithAttatchmentByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<SupportTicket>>(Array.Empty<SupportTicket>());
        public Task<IEnumerable<SupportTicket>> FindWithAttachmentsNoTrackingAsync(Expression<Func<SupportTicket, bool>> predicate) => _repo.FindNoTrackingAsync(predicate);
    }
}
