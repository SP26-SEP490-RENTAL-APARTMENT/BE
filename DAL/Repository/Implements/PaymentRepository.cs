using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class PaymentRepository : Repository<Payment>, IPaymentRepository
    {
        public PaymentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetByLandlordAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = _context.Payments.AsQueryable();

            // Payments from bookings where the apartment belongs to the landlord
            var bookingQuery =
                from p in _context.Payments
                join b in _context.Bookings on p.RelatedEntityId equals (Guid?)b.BookingId
                join a in _context.Apartments on b.ApartmentId equals a.ApartmentId
                where p.RelatedEntityType == "booking" && a.LandlordId == landlordId
                select p;

            // Payments from landlord subscriptions
            var subscriptionQuery =
                from p in _context.Payments
                join s in _context.LandlordSubscriptions on p.RelatedEntityId equals (Guid?)s.SubscriptionId
                where p.RelatedEntityType == "host_subscription" && s.LandlordId == landlordId
                select p;

            query = bookingQuery.Union(subscriptionQuery);

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaidAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt <= toDate.Value);
            }

            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetByTenantAsync(
            Guid tenantId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query =
                from p in _context.Payments
                join b in _context.Bookings on p.RelatedEntityId equals (Guid?)b.BookingId
                where p.RelatedEntityType == "booking" && b.TenantId == tenantId
                select p;

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaidAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt <= toDate.Value);
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