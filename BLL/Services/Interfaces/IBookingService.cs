using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IBookingService : IBaseService<Booking>
{
	Task<BookingQuoteResponseDto> GetQuoteAsync(BookingQuoteRequestDto dto);
	Task<Booking> CreateWithQuoteAsync(CreateBookingRequestDto requestDto, Guid tenantId);
	Task<bool> HasOutstandingUnpaidBookingAsync(Guid tenantId, Guid? requesterId = null, string? requesterRole = null);
	Task<Booking> MarkDepositPaidAsync(Guid bookingId);
	Task<Booking> MarkBalancePaidAsync(Guid bookingId);
	Task<BookingRefundResponseDto> RefundBookingAsync(Guid bookingId, Guid requesterId, RequestBookingRefundDto dto);
	Task<TemporaryResidenceReport> SubmitResidenceReportAsync(Guid bookingId, Guid landlordUserId, SubmitResidenceReportDto dto);
	Task<TemporaryResidenceReportDetailsDto> GetResidenceReportDetailsAsync(Guid bookingId, Guid requesterUserId);
	Task<IReadOnlyList<ResidenceReportOccupantDto>> GetOccupantsAsync(Guid bookingId, Guid tenantUserId);
	Task<ResidenceReportOccupantDto> AddOccupantAsync(Guid bookingId, Guid tenantUserId, AddBookingOccupantDto dto);
	Task<ResidenceReportOccupantDto> UpdateOccupantAsync(Guid bookingId, Guid tenantUserId, int occupantOrder, UpdateBookingOccupantDto dto);
	Task RemoveOccupantAsync(Guid bookingId, Guid tenantUserId, int occupantOrder);
	Task<IReadOnlyList<ResidenceReportOccupantDto>> FillOccupantsManuallyAsync(Guid bookingId, Guid tenantUserId, FillBookingOccupantsDto dto);
	Task<(IEnumerable<Booking> Items, int TotalCount)> GetLandlordBookingHistoryAsync(
		Guid landlordId,
		int page,
		int pageSize,
		string? sortBy = null,
		string? sortOrder = null,
		string? search = null,
		DateTime? fromDate = null,
		DateTime? toDate = null);
	
	/// <summary>
	/// Records the actual check-in time for a booking.
	/// Validates time range (±1 day from booking check-in date), sets IsEarlyCheckIn flag,
	/// calculates early check-in fee if applicable, and notifies landlord.
	/// Prevents re-recording within 24-hour window (throws exception after correction grace period).
	/// </summary>
	Task<BookingCheckTimeResponseDto> RecordCheckInAsync(Guid bookingId, RecordCheckInDto dto, Guid recordedBy);

	/// <summary>
	/// Records the actual check-out time for a booking.
	/// Validates that ActualCheckIn exists first, validates time range (±1 day from booking check-out date),
	/// sets IsLateCheckOut flag, calculates late check-out fee if applicable, updates booking to "completed",
	/// and notifies both landlord and tenant with fee details.
	/// </summary>
	Task<BookingCheckTimeResponseDto> RecordCheckOutAsync(Guid bookingId, RecordCheckOutDto dto, Guid recordedBy);

	/// <summary>
	/// Retrieves full check-time record with history and editability flag for a booking.
	/// </summary>
	Task<BookingCheckTimeResponseDto> GetCheckTimeDetailsAsync(Guid bookingId, Guid? requesterId = null);

	/// <summary>
	/// Allows tenant to confirm or dispute recorded check-in/check-out values.
	/// Dispute action sets booking status to disputed.
	/// </summary>
	Task<BookingCheckTimeResponseDto> RespondToCheckTimeAsync(Guid bookingId, Guid tenantId, RespondBookingCheckTimeDto dto);

	/// <summary>
	/// Allows staff/admin to resolve a tenant check-time dispute.
	/// </summary>
	Task<BookingCheckTimeResponseDto> ResolveCheckTimeDisputeAsync(Guid bookingId, Guid resolvedBy, ResolveBookingCheckTimeDisputeDto dto);

	/// <summary>
	/// Manually settles a booking check-time fee after payment or waiver.
	/// </summary>
	Task<BookingCheckTimeResponseDto> SettleCheckTimeFeeAsync(Guid bookingId, Guid settledBy, SettleBookingCheckTimeFeeDto dto);

	/// <summary>
	/// Allows landlord to submit payment confirmation for a check-time fee marked as due.
	/// Creates a record for staff/admin verification with payment evidence.
	/// </summary>
	Task<BookingCheckTimeResponseDto> SubmitPaymentConfirmationAsync(Guid bookingId, Guid landlordId, LandlordPaymentConfirmationDto dto);

	/// <summary>
	/// Allows tenant to pay a locked checkout claim fee.
	/// </summary>
	Task<BookingCheckTimeResponseDto> PayClaimFeeAsync(Guid bookingId, Guid tenantId, PayClaimFeeDto dto);

	/// <summary>
	/// Marks a booking as no-show when tenant did not check in.
	/// </summary>
	Task<BookingCheckTimeResponseDto> MarkNoShowAsync(Guid bookingId, Guid actorId, MarkNoShowDto dto);

	/// <summary>
	/// Closes a booking with missing check-out record.
	/// </summary>
	Task<BookingCheckTimeResponseDto> CloseMissingCheckOutAsync(Guid bookingId, Guid actorId, CloseMissingCheckOutDto dto);

	/// <summary>
	/// Background automation for claim expiry, no-show detection, and missing check-out closure.
	/// </summary>
	Task ProcessCheckTimeAutomationAsync();

	/// <summary>
	/// Retrieves the availability calendar for an apartment showing available and unavailable date ranges.
	/// Returns a 90-day calendar by default (customizable via startDate/endDate parameters).
	/// Anonymous users see availability only; landlord/owner roles see booking IDs and statuses for blocked periods.
	/// All dates are in UTC.
	/// </summary>
	Task<AvailabilityCalendarResponseDto> GetAvailabilityCalendarAsync(
		Guid apartmentId,
		DateTime? startDate = null,
		DateTime? endDate = null,
		Guid? requesterId = null,
		string? requesterRole = null);

	Task<SetApartmentAvailabilityResponseDto> SetApartmentAvailabilityAsync(
		Guid apartmentId,
		Guid landlordId,
		SetApartmentAvailabilityRequestDto dto);

	Task<RemoveApartmentAvailabilityResponseDto> RemoveApartmentAvailabilityAsync(
		Guid apartmentId,
		Guid landlordId,
		RemoveApartmentAvailabilityRequestDto dto);

	Task<IReadOnlyList<OccupiedRoomAlternativeOptionDto>> FindAlternativeApartmentsAsync(Guid bookingId, int maxResults = 5, int? radiusMeters = null);

	Task<BookingOfferResponseDto> CreateAlternativeOfferAsync(
		Guid bookingId,
		Guid alternativeApartmentId,
		Guid? staffUserId,
		string? reason = null,
		int? expiresInHours = null);

	Task<IReadOnlyList<BookingOfferResponseDto>> GetTenantActiveOffersAsync(Guid tenantId);

	Task<BookingOfferResponseDto> RespondToAlternativeOfferAsync(Guid offerId, Guid tenantId, bool accepted, string? notes = null);

	Task<ConfirmOccupiedIncidentPenaltyResponseDto> ConfirmOccupiedIncidentPenaltyAsync(
		Guid bookingId,
		Guid confirmedBy,
		Guid? ticketId = null,
		string? notes = null);

	/// <summary>
	/// Lists all outstanding check-time fees for a user across multiple bookings.
	/// Staff/admin can query any user; tenants can only query their own fees.
	/// Returns all bookings with non-zero fees that haven't been fully settled.
	/// </summary>
	Task<OutstandingCheckTimeFeesResponseDto> GetOutstandingCheckTimeFeesAsync(Guid userId, Guid? requesterId = null, string? requesterRole = null);

	/// <summary>
	/// Lists all outstanding check-time fees for a landlord across all their properties.
	/// Staff/admin can query any landlord; landlords can only query their own fees.
	/// Returns all bookings with non-zero fees that haven't been fully settled, aggregated by tenant.
	/// </summary>
	Task<LandlordOutstandingCheckTimeFeesResponseDto> GetLandlordOutstandingCheckTimeFeesAsync(Guid landlordId, Guid? requesterId = null, string? requesterRole = null);

	/// <summary>
	/// Lists all bookings that have been reported by tenants (disputes or support tickets).
	/// Returns bookings with disputed check-time status or associated support tickets.
	/// Admin-only access to view reported bookings across all properties.
	/// </summary>
	Task<(IEnumerable<ReportedBookingDto> Items, int TotalCount)> GetReportedBookingsAsync(
		int page = 1,
		int pageSize = 10,
		string? sortBy = null,
		string? sortOrder = null,
		string? search = null,
		DateTime? fromDate = null,
		DateTime? toDate = null);
}
