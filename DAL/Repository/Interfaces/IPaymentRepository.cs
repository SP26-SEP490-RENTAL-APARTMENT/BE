using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<decimal> GetLandlordRevenueTotalAsync(
            Guid landlordId,
            DateTime? fromDate = null,
            DateTime? toDate = null);

        Task<(IEnumerable<Payment> Items, int TotalCount)> GetByLandlordAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null);

        Task<(IEnumerable<Payment> Items, int TotalCount)> GetByTenantAsync(
            Guid tenantId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null);
    }
}