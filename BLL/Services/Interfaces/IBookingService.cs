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
}
