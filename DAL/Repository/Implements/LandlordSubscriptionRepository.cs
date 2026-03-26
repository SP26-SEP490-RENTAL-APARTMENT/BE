using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class LandlordSubscriptionRepository : Repository<LandlordSubscription>, ILandlordSubscriptionRepository
    {
        public LandlordSubscriptionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<LandlordSubscription> Items, int TotalCount)> GetByLandlordAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = _context.LandlordSubscriptions
                .Where(s => s.LandlordId == landlordId);

            // Basic search on status if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowered = search.ToLower();
                query = query.Where(s => s.Status != null && s.Status.ToLower().Contains(lowered));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(s => s.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(s => s.CreatedAt <= toDate.Value);
            }

            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}