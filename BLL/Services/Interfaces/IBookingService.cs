using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IBookingService : IBaseService<Booking>
{
	Task<BookingQuoteResponseDto> GetQuoteAsync(BookingQuoteRequestDto dto);
	Task<Booking> CreateWithQuoteAsync(CreateBookingRequestDto requestDto, Guid tenantId);
	Task<Booking> MarkDepositPaidAsync(Guid bookingId);
	Task<Booking> MarkBalancePaidAsync(Guid bookingId);
	Task<TemporaryResidenceReport> SubmitResidenceReportAsync(Guid bookingId, Guid landlordUserId, SubmitResidenceReportDto dto);
	Task<TemporaryResidenceReportDetailsDto> GetResidenceReportDetailsAsync(Guid bookingId, Guid requesterUserId);
	Task<(IEnumerable<Booking> Items, int TotalCount)> GetLandlordBookingHistoryAsync(
		Guid landlordId,
		int page,
		int pageSize,
		string? sortBy = null,
		string? sortOrder = null,
		string? search = null,
		DateTime? fromDate = null,
		DateTime? toDate = null);
}
