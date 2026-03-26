using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IBookingRepository : IRepository<Booking>
    {
        Task<(IEnumerable<Booking> Items, int TotalCount)> GetByLandlordAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null);
    }
}