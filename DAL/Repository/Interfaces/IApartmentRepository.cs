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
        // in IApartmentRepository
        Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            IEnumerable<string>? allowedColumns = null,
            Dictionary<string, string>? filters = null);
        Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(
            int page,
            int pageSize,
            Guid landlordId,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            IEnumerable<string>? allowedColumns = null,
            Dictionary<string, string>? filters = null);
        
        Task<(IEnumerable<Apartment> Items, int TotalCount)> GetApartmentByLandlordIdAsync(Guid landlordId, int page, int pageSize, string? sortBy, string? sortOrder, string? search, Dictionary<string, string>? filters, string[] allowedColumns);
    }
}