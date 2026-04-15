using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class ReviewRepository : Repository<Review>, IReviewRepository
    {
        public ReviewRepository(AppDbContext context) : base(context)
        {
        }

        public override async Task<Review?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(r => r.Reviewer)
                .Include(r => r.Reviewed)
                .FirstOrDefaultAsync(r => r.ReviewId == id);
        }

        public override async Task<(IEnumerable<Review> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _dbSet
                .Include(r => r.Reviewer)
                .Include(r => r.Reviewed)
                .AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public override async Task<IEnumerable<Review>> FindAsync(System.Linq.Expressions.Expression<Func<Review, bool>> predicate)
        {
            return await _dbSet
                .Include(r => r.Reviewer)
                .Include(r => r.Reviewed)
                .Where(predicate)
                .ToListAsync();
        }
    }
}