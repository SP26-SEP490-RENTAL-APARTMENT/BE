using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements;

public class CheckTimeRequestRepository : Repository<CheckTimeRequest>, ICheckTimeRequestRepository
{
    public CheckTimeRequestRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<CheckTimeRequest?> FindByBookingAndTypeAsync(Guid bookingId, string requestType)
    {
        return await _context.CheckTimeRequests
            .Where(r => r.BookingId == bookingId && r.RequestType == requestType)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<CheckTimeRequest>> FindExpiredAsync()
    {
        return await _context.CheckTimeRequests
            .Where(r => r.Status == "CounterOffered" && r.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
    }

    public async Task<CheckTimeRequest?> FindByIdWithBookingAsync(Guid requestId)
    {
        return await _context.CheckTimeRequests
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.Id == requestId);
    }

    public async Task<IEnumerable<CheckTimeRequest>> GetPendingForBookingAsync(Guid bookingId)
    {
        return await _context.CheckTimeRequests
            .Where(r => r.BookingId == bookingId && (r.Status == "Pending" || r.Status == "CounterOffered"))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> HasPendingOrCounterOfferAsync(Guid bookingId, string requestType)
    {
        return await _context.CheckTimeRequests
            .AnyAsync(r => r.BookingId == bookingId 
                && r.RequestType == requestType 
                && (r.Status == "Pending" || r.Status == "CounterOffered"));
    }

    public async Task<IEnumerable<Booking>> FindOverlappingBookingsAsync(Guid apartmentId, DateTime targetDate, string requestType)
    {
        // For EarlyCheckIn: find bookings that check out on or before targetDate
        // For LateCheckOut: find bookings that check in on or before targetDate
        if (requestType == "EarlyCheckIn")
        {
            return await _context.Bookings
                .Include(b => b.BookingCheckTime)
                .Where(b => b.ApartmentId == apartmentId)
                .Where(b => b.BookingCheckTime != null && 
                           (b.BookingCheckTime.ScheduledCheckOut.Date >= targetDate.AddDays(-1) ||
                            b.BookingCheckTime.ActualCheckOut != null && 
                            b.BookingCheckTime.ActualCheckOut.Value.Date >= targetDate.AddDays(-1)))
                .ToListAsync();
        }
        else // LateCheckOut
        {
            return await _context.Bookings
                .Include(b => b.BookingCheckTime)
                .Where(b => b.ApartmentId == apartmentId)
                .Where(b => b.BookingCheckTime != null && 
                           b.BookingCheckTime.ScheduledCheckIn <= targetDate)
                .ToListAsync();
        }
    }
}
