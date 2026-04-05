using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class RoomRepository : Repository<Room>, IRoomRepository
    {
        public RoomRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<Room> Items, int TotalCount)> GetByApartmentIdAsync(
            Guid apartmentId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        )
        {
            var query = _dbSet.Where(r => r.ApartmentId == apartmentId);

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<Room> Items, int TotalCount)> GetByLandlordIdAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        )
        {
            var query = _dbSet.Where(r => r.Apartment.LandlordId == landlordId);

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (items, totalCount);
        }
    }
}