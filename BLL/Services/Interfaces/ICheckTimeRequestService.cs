using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface ICheckTimeRequestService
{
    /// <summary>
    /// Create a new check-time request from guest.
    /// </summary>
    Task<CheckTimeRequestResponseDto> CreateAsync(Guid bookingId, Guid guestId, CreateCheckTimeRequestDto dto);

    /// <summary>
    /// Landlord counter-offers a different time and/or fee.
    /// </summary>
    Task<CheckTimeRequestResponseDto> CounterOfferAsync(Guid requestId, Guid hostId, CounterCheckTimeDto dto);

    /// <summary>
    /// Guest accepts a counter-offer (calls ApproveAsync internally if no fee).
    /// </summary>
    Task<CheckTimeRequestResponseDto> AcceptCounterAsync(Guid requestId, Guid guestId, AcceptCounterDto? dto = null);

    /// <summary>
    /// Landlord approves a check-time request (triggers payment if fee > 0).
    /// </summary>
    Task<CheckTimeRequestResponseDto> ApproveAsync(Guid requestId, Guid hostId, ApproveCheckTimeDto dto);

    /// <summary>
    /// Landlord rejects a check-time request.
    /// </summary>
    Task<CheckTimeRequestResponseDto> RejectAsync(Guid requestId, Guid hostId, RejectCheckTimeDto? dto = null);

    /// <summary>
    /// Auto-expire stale counter-offers (Status="CounterOffered" and ExpiresAt < now).
    /// </summary>
    Task ExpireStaleRequestsAsync();

    /// <summary>
    /// Check if a requested time can be approved without conflicts.
    /// </summary>
    Task<bool> CanApproveAsync(Guid bookingId, string requestType, DateTime requestedTime);

    /// <summary>
    /// Get check-time request by ID with ownership verification.
    /// </summary>
    Task<CheckTimeRequestResponseDto?> GetByIdAsync(Guid requestId, Guid? requesterId = null);

    /// <summary>
    /// Get all pending/counter-offered requests for a booking.
    /// </summary>
    Task<IEnumerable<CheckTimeRequestResponseDto>> GetPendingForBookingAsync(Guid bookingId);

    /// <summary>
    /// Mark payment as complete for an approved request (for webhook callback).
    /// </summary>
    Task<CheckTimeRequestResponseDto> MarkPaymentCompleteAsync(Guid requestId);
}
