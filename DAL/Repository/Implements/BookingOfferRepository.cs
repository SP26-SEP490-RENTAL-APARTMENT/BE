using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class BookingOfferRepository : Repository<BookingOffer>, IBookingOfferRepository
    {
        public BookingOfferRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<BookingOffer?> GetOfferWithDetailsAsync(Guid offerId)
        {
            return await _context.Set<BookingOffer>()
                .Include(o => o.OriginalBooking)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.Room)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.Amenities)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.ApartmentMedia)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.Landlord)
                        .ThenInclude(l => l.LandlordNavigation)
                .FirstOrDefaultAsync(o => o.OfferId == offerId);
        }

        public async Task<IEnumerable<BookingOffer>> GetPendingOffersForTenantAsync(Guid tenantId, DateTime nowUtc)
        {
            return await _context.Set<BookingOffer>()
                .Where(o =>
                    o.TenantId == tenantId &&
                    o.Status == "pending" &&
                    (!o.ExpiresAt.HasValue || o.ExpiresAt > nowUtc))
                .Include(o => o.OriginalBooking)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.Room)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.Amenities)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.ApartmentMedia)
                .Include(o => o.AlternativeApartment)
                    .ThenInclude(a => a.Landlord)
                        .ThenInclude(l => l.LandlordNavigation)
                .OrderBy(o => o.ExpiresAt)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<BookingOffer>> GetPendingOffersByBookingAsync(Guid bookingId, DateTime nowUtc)
        {
            return await _context.Set<BookingOffer>()
                .Where(o =>
                    o.OriginalBookingId == bookingId &&
                    o.Status == "pending" &&
                    (!o.ExpiresAt.HasValue || o.ExpiresAt > nowUtc))
                .ToListAsync();
        }
    }
}
