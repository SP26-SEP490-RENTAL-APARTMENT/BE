using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class PropertyInspectionRepository : Repository<PropertyInspection>, IPropertyInspectionRepository
    {
        public PropertyInspectionRepository(AppDbContext context) : base(context)
        {
        }

        public override async Task<PropertyInspection?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(i => i.Apartment)
                .FirstOrDefaultAsync(i => i.InspectionId == id);
        }

        public override async Task<(IEnumerable<PropertyInspection> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        )
        {
            var query = _dbSet
                .Include(i => i.Apartment)
                .AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }
    }
}