using DAL.Models;

namespace DAL.Repository.Interfaces
{
    public interface IBookingOfferRepository : IRepository<BookingOffer>
    {
        Task<BookingOffer?> GetOfferWithDetailsAsync(Guid offerId);

        Task<IEnumerable<BookingOffer>> GetPendingOffersForTenantAsync(Guid tenantId, DateTime nowUtc);

        Task<IEnumerable<BookingOffer>> GetPendingOffersByBookingAsync(Guid bookingId, DateTime nowUtc);
    }
}
