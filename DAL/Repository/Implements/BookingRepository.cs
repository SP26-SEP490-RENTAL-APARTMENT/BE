using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class BookingRepository : Repository<Booking>, IBookingRepository
    {
        public BookingRepository(AppDbContext context) : base(context)
        {
        }

        private static IQueryable<Booking> ApplyBookingSearch(IQueryable<Booking> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return query;
            }

            var lowered = search.ToLower();
            return query.Where(b =>
                (b.Status != null && b.Status.ToLower().Contains(lowered)) ||
                (b.Tenant != null &&
                 b.Tenant.TenantNavigation != null &&
                 b.Tenant.TenantNavigation.FullName != null &&
                 b.Tenant.TenantNavigation.FullName.ToLower().Contains(lowered)));
        }

        public override async Task<Booking?> GetByIdAsync(Guid id)
        {
            return await _context.Bookings
                .Include(b => b.Tenant)
                .ThenInclude(t => t.TenantNavigation)
                .Include(b => b.BookingCheckTime)
                .FirstOrDefaultAsync(b => b.BookingId == id);
        }

        public override async Task<(IEnumerable<Booking> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _context.Bookings
                .Include(b => b.Tenant)
                .ThenInclude(t => t.TenantNavigation)
                .Include(b => b.BookingCheckTime)
                .AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplyBookingSearch(query, search);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<(IEnumerable<Booking> Items, int TotalCount)> GetByLandlordAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query =
                from b in _context.Bookings
                    .Include(b => b.Tenant)
                    .ThenInclude(t => t.TenantNavigation)
                    .Include(b => b.BookingCheckTime)
                    .AsQueryable()
                join a in _context.Apartments on b.ApartmentId equals a.ApartmentId
                where a.LandlordId == landlordId
                select b;

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = ApplyBookingSearch(query, search);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt <= toDate.Value);
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