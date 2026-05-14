using BLL.Services.Interfaces;
using AutoMapper;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NotificationType = Common.Enums.Notification;

namespace BLL.Services.Implements;

public class CheckTimeRequestService : ICheckTimeRequestService
{
    private readonly ICheckTimeRequestRepository _checkTimeRequestRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IRepository<DAL.Models.Notification> _notificationRepository;
    private readonly IRepository<Booking> _bookingBaseRepository;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private const int DefaultCounterOfferExpiryHours = 24;
    private const int DefaultRequestCreationExpiryHours = 48;
    private const int DefaultHousekeepingBufferMinutes = 120;

    public CheckTimeRequestService(
        ICheckTimeRequestRepository checkTimeRequestRepository,
        IBookingRepository bookingRepository,
        IApartmentRepository apartmentRepository,
        IRepository<DAL.Models.Notification> notificationRepository,
        IRepository<Booking> bookingBaseRepository,
        IConfiguration configuration,
        IMapper mapper)
    {
        _checkTimeRequestRepository = checkTimeRequestRepository;
        _bookingRepository = bookingRepository;
        _apartmentRepository = apartmentRepository;
        _notificationRepository = notificationRepository;
        _bookingBaseRepository = bookingBaseRepository;
        _configuration = configuration;
        _mapper = mapper;
    }

    public async Task<CheckTimeRequestResponseDto> CreateAsync(Guid bookingId, Guid guestId, CreateCheckTimeRequestDto dto)
    {
        // Validate booking exists and is in confirmed/paid status
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new KeyNotFoundException($"Booking {bookingId} not found.");

        if (booking.Status != "Confirmed" && booking.Status != "Paid")
            throw new InvalidOperationException($"Cannot create check-time request for booking in status '{booking.Status}'.");

        // Verify guest ownership
        if (booking.TenantId != guestId)
            throw new UnauthorizedAccessException("Guest does not own this booking.");

        // Prevent duplicate pending request
        var hasPending = await _checkTimeRequestRepository.HasPendingOrCounterOfferAsync(bookingId, dto.RequestType);
        if (hasPending)
            throw new InvalidOperationException($"Booking already has a pending {dto.RequestType} request.");

        // Create request
        var request = new CheckTimeRequest
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            RequestType = dto.RequestType,
            RequestedTime = dto.RequestedTime,
            GuestReason = dto.GuestReason,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(DefaultRequestCreationExpiryHours)
        };

        await _checkTimeRequestRepository.AddAsync(request);
        await _checkTimeRequestRepository.SaveChangesAsync();

        // Notify landlord
        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            await CreateCheckTimeNotificationAsync(
                apartment.LandlordId,
                "check_time_requested",
                $"Guest requested {dto.RequestType}",
                $"Guest requested {dto.RequestType} at {dto.RequestedTime:yyyy-MM-dd HH:mm}",
                request.Id);
        }

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    public async Task<CheckTimeRequestResponseDto> CounterOfferAsync(Guid requestId, Guid hostId, CounterCheckTimeDto dto)
    {
        var request = await _checkTimeRequestRepository.FindByIdWithBookingAsync(requestId);
        if (request == null)
            throw new KeyNotFoundException($"Request {requestId} not found.");

        // Verify host ownership
        var apartment = await _apartmentRepository.GetByIdAsync(request.Booking.ApartmentId);
        if (apartment?.LandlordId != hostId)
            throw new UnauthorizedAccessException("Host does not own this apartment.");

        if (request.Status != "Pending")
            throw new InvalidOperationException($"Cannot counter-offer request in status '{request.Status}'.");

        // Update request
        request.CounterOfferedTime = dto.CounterOfferedTime;
        request.CounterOfferedFee = dto.CounterOfferedFee ?? 0;
        request.HostResponse = dto.HostResponse;
        request.Status = "CounterOffered";
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedById = hostId;
        request.ExpiresAt = DateTime.UtcNow.AddHours(DefaultCounterOfferExpiryHours);

        _checkTimeRequestRepository.Update(request);
        await _checkTimeRequestRepository.SaveChangesAsync();

        // Notify guest
        await CreateCheckTimeNotificationAsync(
            request.Booking.TenantId,
            "check_time_countered",
            $"Landlord countered your {request.RequestType} request",
            $"Offered time: {dto.CounterOfferedTime:yyyy-MM-dd HH:mm}, Fee: {dto.CounterOfferedFee}. Expires in 24 hours.",
            request.Id);

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    public async Task<CheckTimeRequestResponseDto> AcceptCounterAsync(Guid requestId, Guid guestId, AcceptCounterDto? dto = null)
    {
        var request = await _checkTimeRequestRepository.FindByIdWithBookingAsync(requestId);
        if (request == null)
            throw new KeyNotFoundException($"Request {requestId} not found.");

        // Verify guest ownership
        if (request.Booking.TenantId != guestId)
            throw new UnauthorizedAccessException("Guest does not own this request.");

        if (request.Status != "CounterOffered")
            throw new InvalidOperationException($"Can only accept counter-offers, current status: {request.Status}");

        // Check if expired
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Counter-offer has expired.");

        // Call ApproveAsync with counter values
        var approveDto = new ApproveCheckTimeDto
        {
            AgreedFee = request.CounterOfferedFee,
            HostResponse = request.HostResponse
        };

        // Internally approve with counter values
        request.AgreedTime = request.CounterOfferedTime;
        request.AgreedFee = request.CounterOfferedFee;
        request.Status = "Approved";
        request.ProcessedAt = DateTime.UtcNow;

        _checkTimeRequestRepository.Update(request);
        await _checkTimeRequestRepository.SaveChangesAsync();

        // Notify both parties
        await CreateCheckTimeNotificationAsync(
            request.Booking.TenantId,
            "check_time_approved",
            $"Your {request.RequestType} request was approved",
            $"Approved time: {request.AgreedTime:yyyy-MM-dd HH:mm}",
            request.Id);

        var apartment = await _apartmentRepository.GetByIdAsync(request.Booking.ApartmentId);
        if (apartment != null)
        {
            await CreateCheckTimeNotificationAsync(
                apartment.LandlordId,
                "check_time_accepted",
                "Guest accepted your counter-offer",
                $"Request approved for {request.AgreedTime:yyyy-MM-dd HH:mm}",
                request.Id);
        }

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    public async Task<CheckTimeRequestResponseDto> ApproveAsync(Guid requestId, Guid hostId, ApproveCheckTimeDto dto)
    {
        var request = await _checkTimeRequestRepository.FindByIdWithBookingAsync(requestId);
        if (request == null)
            throw new KeyNotFoundException($"Request {requestId} not found.");

        // Verify host ownership
        var apartment = await _apartmentRepository.GetByIdAsync(request.Booking.ApartmentId);
        if (apartment?.LandlordId != hostId)
            throw new UnauthorizedAccessException("Host does not own this apartment.");

        if (request.Status != "Pending" && request.Status != "CounterOffered")
            throw new InvalidOperationException($"Cannot approve request in status '{request.Status}'.");

        // Check availability
        var canApprove = await CanApproveAsync(request.BookingId, request.RequestType, request.RequestedTime);
        if (!canApprove)
        {
            // Check with counter-offered time if available
            if (request.CounterOfferedTime.HasValue)
            {
                canApprove = await CanApproveAsync(request.BookingId, request.RequestType, request.CounterOfferedTime.Value);
            }

            if (!canApprove)
                throw new InvalidOperationException("Cannot approve due to conflicting bookings or schedule constraints.");
        }

        // Determine agreed time
        var agreedTime = request.CounterOfferedTime ?? request.RequestedTime;
        var agreedFee = dto.AgreedFee ?? (request.CounterOfferedFee ?? 0);

        // Update request
        request.AgreedTime = agreedTime;
        request.AgreedFee = agreedFee;
        request.Status = "Approved";
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedById = hostId;
        request.HostResponse = dto.HostResponse;

        _checkTimeRequestRepository.Update(request);
        await _checkTimeRequestRepository.SaveChangesAsync();

        // Notify guest
        await CreateCheckTimeNotificationAsync(
            request.Booking.TenantId,
            "check_time_approved",
            $"Your {request.RequestType} request was approved",
            $"Approved time: {agreedTime:yyyy-MM-dd HH:mm}. Fee: {agreedFee}",
            request.Id);

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    public async Task<CheckTimeRequestResponseDto> RejectAsync(Guid requestId, Guid hostId, RejectCheckTimeDto? dto = null)
    {
        var request = await _checkTimeRequestRepository.FindByIdWithBookingAsync(requestId);
        if (request == null)
            throw new KeyNotFoundException($"Request {requestId} not found.");

        // Verify host ownership
        var apartment = await _apartmentRepository.GetByIdAsync(request.Booking.ApartmentId);
        if (apartment?.LandlordId != hostId)
            throw new UnauthorizedAccessException("Host does not own this apartment.");

        if (request.Status != "Pending" && request.Status != "CounterOffered")
            throw new InvalidOperationException($"Cannot reject request in status '{request.Status}'.");

        // Update request
        request.Status = "Rejected";
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedById = hostId;
        request.HostResponse = dto?.Reason;

        _checkTimeRequestRepository.Update(request);
        await _checkTimeRequestRepository.SaveChangesAsync();

        // Notify guest
        await CreateCheckTimeNotificationAsync(
            request.Booking.TenantId,
            "check_time_rejected",
            $"Your {request.RequestType} request was rejected",
            $"Reason: {request.HostResponse ?? "Not specified"}",
            request.Id);

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    public async Task ExpireStaleRequestsAsync()
    {
        var expiredRequests = await _checkTimeRequestRepository.FindExpiredAsync();

        foreach (var request in expiredRequests)
        {
            request.Status = "Expired";
            _checkTimeRequestRepository.Update(request);

            // Notify guest
            await CreateCheckTimeNotificationAsync(
                request.Booking.TenantId,
                "check_time_expired",
                "Your counter-offer has expired",
                $"Your {request.RequestType} counter-offer expired at {request.ExpiresAt}",
                request.Id);
        }

        if (expiredRequests.Any())
            await _checkTimeRequestRepository.SaveChangesAsync();
    }

    public async Task<bool> CanApproveAsync(Guid bookingId, string requestType, DateTime requestedTime)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            return false;

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
            return false;

        // Get all overlapping bookings on the same apartment
        var overlappingBookings = await _checkTimeRequestRepository.FindOverlappingBookingsAsync(
            apartment.ApartmentId,
            requestedTime,
            requestType);

        var housekeepingBuffer = DefaultHousekeepingBufferMinutes;

        foreach (var otherBooking in overlappingBookings.Where(b => b.BookingId != bookingId))
        {
            if (otherBooking.BookingCheckTime == null)
                continue;

            if (requestType == "EarlyCheckIn")
            {
                // Check if previous booking's check-out is too close to requested early check-in
                var actualCheckOut = otherBooking.BookingCheckTime.ActualCheckOut ?? otherBooking.BookingCheckTime.ScheduledCheckOut;
                var bufferEnd = actualCheckOut.AddMinutes(housekeepingBuffer);

                if (requestedTime < bufferEnd)
                    return false; // Conflict with housekeeping time
            }
            else if (requestType == "LateCheckOut")
            {
                // Check if next booking's check-in is too close to requested late check-out
                if (otherBooking.BookingCheckTime.ScheduledCheckIn <= requestedTime.AddMinutes(housekeepingBuffer))
                    return false; // Conflict with next check-in
            }
        }

        return true;
    }

    public async Task<CheckTimeRequestResponseDto?> GetByIdAsync(Guid requestId, Guid? requesterId = null)
    {
        var request = await _checkTimeRequestRepository.FindByIdWithBookingAsync(requestId);
        if (request == null)
            return null;

        // Verify ownership if requester specified
        if (requesterId.HasValue)
        {
            var apartment = await _apartmentRepository.GetByIdAsync(request.Booking.ApartmentId);
            var isGuest = request.Booking.TenantId == requesterId;
            var isLandlord = apartment?.LandlordId == requesterId;

            if (!isGuest && !isLandlord)
                throw new UnauthorizedAccessException("You don't have access to this request.");
        }

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    public async Task<IEnumerable<CheckTimeRequestResponseDto>> GetPendingForBookingAsync(Guid bookingId)
    {
        var requests = await _checkTimeRequestRepository.GetPendingForBookingAsync(bookingId);
        return requests.Select(r => _mapper.Map<CheckTimeRequestResponseDto>(r));
    }

    public async Task<CheckTimeRequestResponseDto> MarkPaymentCompleteAsync(Guid requestId)
    {
        var request = await _checkTimeRequestRepository.GetByIdAsync(requestId);
        if (request == null)
            throw new KeyNotFoundException($"Request {requestId} not found.");

        if (request.Status != "Approved")
            throw new InvalidOperationException("Only approved requests can have payments marked as complete.");

        // Mark as paid
        request.Status = "Approved"; // Status remains Approved, but fee is now paid

        _checkTimeRequestRepository.Update(request);
        await _checkTimeRequestRepository.SaveChangesAsync();

        return _mapper.Map<CheckTimeRequestResponseDto>(request);
    }

    private async Task CreateCheckTimeNotificationAsync(
        Guid userId,
        string type,
        string title,
        string message,
        Guid requestId)
    {
        var notification = new DAL.Models.Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = requestId,
            ReferenceType = "check_time_request",
            IsRead = false,
            CreatedAt = Common.Utils.VietnamTime.Now
        };

        await _notificationRepository.AddAsync(notification);
        await _notificationRepository.SaveChangesAsync();
    }
}
