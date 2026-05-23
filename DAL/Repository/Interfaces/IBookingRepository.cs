using DAL.Models;
using DAL.Repository.Interfaces;
using Common.DTOs;

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
            DateTime? toDate = null,
            IEnumerable<string>? allowedColumns = null);

        Task<(IEnumerable<ReportResultRowDto> Items, int TotalCount)> GetPagedGroupedReportRowsAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            IReadOnlyList<ReportDimensionRequestDto> dimensions,
            IReadOnlyList<ReportMetricRequestDto> metrics,
            string? searchTerm,
            int page,
            int pageSize,
            Guid? landlordId = null);
    }
}