using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IRoomRepository : IRepository<Room>
    {
        Task<(IEnumerable<Room> Items, int TotalCount)> GetByApartmentIdAsync(
            Guid apartmentId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        );

        Task<(IEnumerable<Room> Items, int TotalCount)> GetByLandlordIdAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        );
    }
}