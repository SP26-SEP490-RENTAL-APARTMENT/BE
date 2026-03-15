using System;
using System.Threading.Tasks;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class ApartmentRepository : Repository<Apartment>, IApartmentRepository
    {
        public ApartmentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Apartment?> GetApartmentWithDetailsAsync(Guid id)
        {
            return await _dbSet
                .Include(a => a.Room)
                .Include(a => a.Amenities)
                .Include(a => a.ApartmentMedia)
                .FirstOrDefaultAsync(a => a.ApartmentId == id);
        }

        public override async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _dbSet.AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(a => a.Room)
                .Include(a => a.Amenities)
                .Include(a => a.ApartmentMedia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}