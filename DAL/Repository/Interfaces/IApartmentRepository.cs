using System;
using System.Threading.Tasks;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IApartmentRepository : IRepository<Apartment>
    {
        Task<Apartment?> GetApartmentWithDetailsAsync(Guid id);
        Task UpdateListingStatusAsync(Guid apartmentId, string status, string bookingStatus);
        Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null,
            DateOnly? checkInDate = null,
            DateOnly? checkOutDate = null);
    }
}