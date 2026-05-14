using DAL.Models;

namespace DAL.Repository.Interfaces;

public interface ICheckTimeRequestRepository : IRepository<CheckTimeRequest>
{
    /// <summary>
    /// Find pending or latest check-time request for a booking by request type
    /// </summary>
    Task<CheckTimeRequest?> FindByBookingAndTypeAsync(Guid bookingId, string requestType);

    /// <summary>
    /// Find all expired counter-offers (Status="CounterOffered" and ExpiresAt < now)
    /// </summary>
    Task<IEnumerable<CheckTimeRequest>> FindExpiredAsync();

    /// <summary>
    /// Fetch check-time request with booking eager-loaded
    /// </summary>
    Task<CheckTimeRequest?> FindByIdWithBookingAsync(Guid requestId);

    /// <summary>
    /// Get all pending requests for a booking
    /// </summary>
    Task<IEnumerable<CheckTimeRequest>> GetPendingForBookingAsync(Guid bookingId);

    /// <summary>
    /// Check if there's an existing pending or counter-offered request for the booking and request type
    /// </summary>
    Task<bool> HasPendingOrCounterOfferAsync(Guid bookingId, string requestType);

    /// <summary>
    /// Find all bookings on the same apartment with overlapping or adjacent check-out dates
    /// </summary>
    Task<IEnumerable<Booking>> FindOverlappingBookingsAsync(Guid apartmentId, DateTime targetDate, string requestType);
}
