using BLL.Services.Interfaces;
using BLL.Exceptions;
using AutoMapper;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using DAL.Repository.Interfaces;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using NotificationType = Common.Enums.Notification;
using ApartmentBookingStatusEnum = Common.Enums.ApartmentBookingStatus;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Common.Settings;
using Microsoft.Extensions.Options;

namespace BLL.Services.Implements;

public class BookingService : BaseService<Booking>, IBookingService
{
    private readonly PayOSClient _payOsClient;
    private const decimal SuggestedDepositRate = 0.30m;
    private const decimal FullPaymentLandlordShareRate = 0.70m;
    private const int MaxBookingDays = 30;
    private const string FeeSettlementStatusNone = "none";
    private const string FeeSettlementStatusDue = "due";
    private const string FeeSettlementStatusDisputed = "disputed";
    private const string FeeSettlementStatusPaid = "paid";
    private const string FeeSettlementStatusWaived = "waived";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> DepositConfirmationLocks = new();
    private const int DefaultOfferExpiryHours = 2;
    private const decimal DefaultPriceTolerancePercent = 0.20m;

    private readonly IPayOSPayoutService _payOSPayoutService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingOfferRepository _bookingOfferRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IApartmentPriceCalendarRepository _apartmentPriceCalendarRepository;
    private readonly IRepository<Package> _packageRepository;
    private readonly IRepository<DAL.Models.Notification> _notificationRepository;
    private readonly IRepository<BookingCheckTime> _bookingCheckTimeRepository;
    private readonly IRepository<BookingCheckTimeStateEvent>? _checkTimeStateEventRepository;
    private readonly IRepository<TemporaryResidenceReport> _temporaryResidenceReportRepository;
    private readonly IRepository<BookingOccupant>? _bookingOccupantRepository;
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<ApartmentAvailability> _apartmentAvailabilityRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IStripeService _stripeService;
    private readonly StripeSettings _stripeSettings;
    private readonly IMomoService _momoService;
    private readonly IIdentityVerificationService _identityVerificationService;
    private readonly ILandlordWalletService _landlordWalletService;
    private readonly IConfiguration _configuration;
    private readonly BookingAdmissionPolicySettings _bookingAdmissionPolicySettings;
    private readonly IMapper _mapper;
    private readonly ICheckTimeRequestRepository? _checkTimeRequestRepository;

    public BookingService(
        IBookingRepository repository,
        IBookingOfferRepository bookingOfferRepository,
        IApartmentRepository apartmentRepository,
        IApartmentPriceCalendarRepository apartmentPriceCalendarRepository,
        IRepository<Package> packageRepository,
        IRepository<DAL.Models.Notification> notificationRepository,
        IRepository<BookingCheckTime> bookingCheckTimeRepository,
        IRepository<TemporaryResidenceReport> temporaryResidenceReportRepository,
        IRepository<Tenant> tenantRepository,
        IRepository<User> userRepository,
        IRepository<ApartmentAvailability> apartmentAvailabilityRepository,
        ISupportTicketRepository supportTicketRepository,
        IRepository<Payment> paymentRepository,
        IStripeService stripeService,
        PayOSClient payOsClient,
        IMomoService momoService,
        IOptions<StripeSettings> stripeSettings,
        IPayOSPayoutService payOSPayoutService,
        IIdentityVerificationService identityVerificationService,
        ILandlordWalletService landlordWalletService,
        IConfiguration configuration,
        IOptions<BookingAdmissionPolicySettings> bookingAdmissionPolicySettings,
        IMapper mapper,
        IRepository<BookingOccupant>? bookingOccupantRepository = null,
        ICheckTimeRequestRepository? checkTimeRequestRepository = null,
        IRepository<BookingCheckTimeStateEvent>? checkTimeStateEventRepository = null) : base(repository)
    {
        _bookingRepository = repository;
        _bookingOfferRepository = bookingOfferRepository;
        _apartmentRepository = apartmentRepository;
        _apartmentPriceCalendarRepository = apartmentPriceCalendarRepository;
        _packageRepository = packageRepository;
        _notificationRepository = notificationRepository;
        _bookingCheckTimeRepository = bookingCheckTimeRepository;
        _temporaryResidenceReportRepository = temporaryResidenceReportRepository;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _apartmentAvailabilityRepository = apartmentAvailabilityRepository;
        _supportTicketRepository = supportTicketRepository;
        _paymentRepository = paymentRepository;
        _stripeService = stripeService;
        _stripeSettings = stripeSettings.Value;
        _momoService = momoService;
        _payOsClient = payOsClient;
        _payOSPayoutService = payOSPayoutService;
        _identityVerificationService = identityVerificationService;
        _landlordWalletService = landlordWalletService;
        _configuration = configuration;
        _bookingAdmissionPolicySettings = bookingAdmissionPolicySettings.Value;
        _mapper = mapper;
        _bookingOccupantRepository = bookingOccupantRepository;
        _checkTimeRequestRepository = checkTimeRequestRepository;
        _checkTimeStateEventRepository = checkTimeStateEventRepository;
    }

    public async Task<BookingResponseDto> MapBookingResponseAsync(Booking booking)
    {
        var response = _mapper.Map<BookingResponseDto>(booking);
        response.Images = await GetApartmentImageUrlsAsync(booking.ApartmentId);
        response.TicketId = await ResolveBookingSupportTicketIdAsync(booking.BookingId, booking.TenantId);
        response.IsRefundable = await IsBookingRefundableForTenantAsync(booking);
        return response;
    }

    private async Task<bool> IsBookingRefundableForTenantAsync(Booking booking)
    {
        if (booking == null) return false;

        // Already finalised -> not refundable
        if (string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If a successful refund already exists -> not refundable
        if (await HasSuccessfulBookingRefundAsync(booking.BookingId))
        {
            return false;
        }

        // Ensure there is at least one original paid payment to refund
        var paidPayments = (await _paymentRepository.FindAsync(p =>
            p.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
            && p.RelatedEntityId == booking.BookingId
            && p.Status == PaymentStatus.success.ToString()
            && p.PaymentType != PaymentTypes.refund.ToString())).ToList();

        if (!paidPayments.Any())
        {
            return false;
        }

        // Need booking check-time to compute hours until check-in
        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        var now = Common.Utils.VietnamTime.Now;
        var hoursUntilCheckIn = (checkTime.ScheduledCheckIn - now).TotalHours;

        // Tenant requests must be >= 24 hours before check-in
        return hoursUntilCheckIn >= 24;
    }

    private async Task<List<string>> GetApartmentImageUrlsAsync(Guid apartmentId)
    {
        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        return apartment?.ApartmentMedia
            .Where(media => !string.IsNullOrWhiteSpace(media.Url))
            .Select(media => media.Url)
            .ToList()
            ?? new List<string>();
    }

    private async Task<Guid?> ResolveBookingSupportTicketIdAsync(Guid bookingId, Guid tenantId)
    {
        var bookingMarker = bookingId.ToString().ToLowerInvariant();
        var tickets = await _supportTicketRepository.FindAsync(ticket =>
            ticket.UserId == tenantId
            && ticket.Category == "booking_issue"
            && ticket.Subject != null
            && ticket.Subject.ToLower().Contains(bookingMarker));

        return tickets
            .OrderByDescending(ticket => ticket.CreatedAt ?? DateTime.MinValue)
            .Select(ticket => (Guid?)ticket.TicketId)
            .FirstOrDefault();
    }

    public async Task<BookingAdmissionEvaluationDto> EvaluateTenantBookingAdmissionAsync(
        Guid tenantId,
        BookingPaymentMode requestedPaymentMode = BookingPaymentMode.partial,
        Guid? requesterId = null,
        string? requesterRole = null)
    {
        var outstandingBookings = (await _bookingRepository.FindAsync(b =>
            b.TenantId == tenantId
            && string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase)
            && (b.DepositPaid != true || b.RemainingAmount > 0m))).ToList();

        var outstandingAmount = outstandingBookings.Sum(b => Math.Max(0m, b.RemainingAmount));
        var unpaidBookingCount = outstandingBookings.Count;

        DateTime? oldestOutstandingAt = null;
        if (unpaidBookingCount > 0)
        {
            oldestOutstandingAt = outstandingBookings
                .Select(GetOutstandingBookingReferenceTimestamp)
                .OrderBy(timestamp => timestamp)
                .First();
        }

        var graceWindowExpiry = oldestOutstandingAt?.AddHours(_bookingAdmissionPolicySettings.GraceWindowHours);
        var now = Common.Utils.VietnamTime.Now;
        var isInsideGraceWindow = graceWindowExpiry.HasValue && now <= graceWindowExpiry.Value;
        var selectedPaymentMode = requestedPaymentMode.ToString();
        var selectedPaymentModeAllowed = unpaidBookingCount == 0
            || _bookingAdmissionPolicySettings.AllowedPaymentModesWhenDebtExists.Any(mode =>
                string.Equals(mode, selectedPaymentMode, StringComparison.OrdinalIgnoreCase));

        var exceedsUnpaidBookingLimit = unpaidBookingCount > _bookingAdmissionPolicySettings.MaxSimultaneousUnpaidConfirmedBookings;
        var hasOutstandingDebt = unpaidBookingCount > 0;

        var allowed = !exceedsUnpaidBookingLimit
            && (!hasOutstandingDebt || (isInsideGraceWindow && selectedPaymentModeAllowed));

        if (IsPolicyBypassRole(requesterRole))
        {
            allowed = true;
        }

        return new BookingAdmissionEvaluationDto
        {
            Allowed = allowed,
            BlockReason = allowed
                ? null
                : BuildAdmissionBlockReason(
                    exceedsUnpaidBookingLimit,
                    unpaidBookingCount,
                    isInsideGraceWindow,
                    selectedPaymentModeAllowed,
                    graceWindowExpiry,
                    selectedPaymentMode),
            OutstandingAmount = outstandingAmount,
            UnpaidBookingCount = unpaidBookingCount,
            GraceWindowExpiry = graceWindowExpiry,
            IsInsideGraceWindow = isInsideGraceWindow,
            SelectedPaymentModeAllowed = selectedPaymentModeAllowed,
            SelectedPaymentMode = selectedPaymentMode,
            OldestUnpaidBookingAgeHours = oldestOutstandingAt.HasValue
                ? Math.Max(0d, (now - oldestOutstandingAt.Value).TotalHours)
                : null
        };
    }

    private static bool IsPolicyBypassRole(string? requesterRole)
    {
        if (string.IsNullOrWhiteSpace(requesterRole))
        {
            return false;
        }

        return string.Equals(requesterRole, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requesterRole, "staff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requesterRole, "appeals", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetOutstandingBookingReferenceTimestamp(Booking booking)
    {
        if (booking.BalanceDueDate.Year > 1)
        {
            return booking.BalanceDueDate.ToDateTime(TimeOnly.MinValue);
        }

        return booking.CreatedAt ?? Common.Utils.VietnamTime.Now;
    }

    private static string BuildAdmissionBlockReason(
        bool exceedsUnpaidBookingLimit,
        int unpaidBookingCount,
        bool isInsideGraceWindow,
        bool selectedPaymentModeAllowed,
        DateTime? graceWindowExpiry,
        string selectedPaymentMode)
    {
        if (exceedsUnpaidBookingLimit)
        {
            return $"You have {unpaidBookingCount} confirmed unpaid booking(s), which exceeds the allowed limit.";
        }

        if (!isInsideGraceWindow && !selectedPaymentModeAllowed)
        {
            return graceWindowExpiry.HasValue
                ? $"You have outstanding unpaid booking(s). The grace window expired at {graceWindowExpiry.Value:yyyy-MM-dd HH:mm:ss} and the selected payment mode '{selectedPaymentMode}' is not allowed while debt exists."
                : $"You have outstanding unpaid booking(s), and the selected payment mode '{selectedPaymentMode}' is not allowed while debt exists.";
        }

        if (!isInsideGraceWindow)
        {
            return graceWindowExpiry.HasValue
                ? $"You have outstanding unpaid booking(s). The grace window expired at {graceWindowExpiry.Value:yyyy-MM-dd HH:mm:ss}."
                : "You have outstanding unpaid booking(s).";
        }

        return $"The selected payment mode '{selectedPaymentMode}' is not allowed while debt exists.";
    }

    public async Task<ConfirmOccupiedIncidentPenaltyResponseDto> ConfirmOccupiedIncidentPenaltyAsync(
        Guid bookingId,
        Guid confirmedBy,
        ConfirmOccupiedIncidentPenaltyRequestDto? dto = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        if (booking.DepositAmount <= 0)
            throw new InvalidOperationException("Booking deposit amount is not valid for penalty calculation.");

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        var ticket = await ResolveOccupiedIncidentTicketAsync(bookingId, booking.TenantId, dto?.TicketId);
        if (ticket == null)
        {
            throw new InvalidOperationException("Occupied incident ticket was not found for this booking.");
        }

        var now = Common.Utils.VietnamTime.Now;
        var existingRefund = await HasSuccessfulBookingRefundAsync(bookingId);
        if (!existingRefund)
        {
            var payOsPayment = (await _paymentRepository.FindAsync(payment =>
                    payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                    && payment.RelatedEntityId == booking.BookingId
                    && payment.Status == PaymentStatus.success.ToString()
                    && payment.PaymentType != PaymentTypes.refund.ToString()
                    && string.Equals(payment.Method, "payos", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(payment => payment.PaidAt ?? DateTime.MinValue)
                .FirstOrDefault();

            var hasPayOsPayment = payOsPayment != null;
            string? payOsReceiverName = null;
            string? payOsBankCode = null;
            string? payOsAccountNumber = null;

            if (hasPayOsPayment)
            {
                payOsBankCode = payOsPayment!.PayerBankBin?.Trim();
                payOsAccountNumber = payOsPayment.PayerAccountNumber?.Trim();

                var tenantUser = await _userRepository.GetByIdAsync(booking.TenantId);
                payOsReceiverName = tenantUser?.FullName?.Trim();

                if (string.IsNullOrWhiteSpace(payOsReceiverName))
                {
                    payOsReceiverName = "PayOS refund recipient";
                }

                if (string.IsNullOrWhiteSpace(payOsBankCode) || string.IsNullOrWhiteSpace(payOsAccountNumber))
                {
                    throw new InvalidOperationException("Stored PayOS payment details are incomplete for refund processing.");
                }
            }

            var refundRequest = new RequestBookingRefundDto
            {
                Reason = "system_cancellation",
                Notes = "Refund processed after occupied incident confirmation.",
                PayOsReceiverName = payOsReceiverName,
                PayOsBankCode = payOsBankCode,
                PayOsAccountNumber = payOsAccountNumber
            };

            var hasPayOsPayoutDetails = !string.IsNullOrWhiteSpace(refundRequest.PayOsReceiverName)
                && !string.IsNullOrWhiteSpace(refundRequest.PayOsBankCode)
                && !string.IsNullOrWhiteSpace(refundRequest.PayOsAccountNumber);

            if (hasPayOsPayment)
            {
                if (!hasPayOsPayoutDetails)
                {
                    throw new InvalidOperationException("PayOS payout details are required to confirm this occupied incident penalty because the booking was paid via PayOS.");
                }

                await RefundBookingViaPayOsAsync(bookingId, booking.TenantId, refundRequest);
            }
            else
            {
                await RefundBookingAsync(bookingId, booking.TenantId, refundRequest);
            }
        }

        var penaltyTransactionId = $"occupied_penalty_{bookingId:N}";
        var existingPenalty = (await _paymentRepository.FindAsync(p => p.TransactionId == penaltyTransactionId)).FirstOrDefault();
        LandlordPenaltyApplicationResultDto settlement;
        if (existingPenalty != null)
        {
            settlement = new LandlordPenaltyApplicationResultDto
            {
                RequestedAmount = booking.DepositAmount,
                DeductedFromAvailable = 0m,
                DeductedFromPending = 0m,
                DebtRecorded = 0m
            };
        }
        else
        {
            settlement = await _landlordWalletService.ApplyOccupiedIncidentPenaltyAsync(apartment.LandlordId, booking.DepositAmount);

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = booking.DepositAmount,
                PaymentType = Common.Enums.PaymentTypes.refund.ToString(),
                PaymentPurpose = Common.Enums.PaymentPurposes.other.ToString(),
                RelatedEntityId = booking.BookingId,
                RelatedEntityType = Common.Enums.PaymentRelatedEntityType.booking.ToString(),
                Method = "landlord_wallet_penalty",
                Status = Common.Enums.PaymentStatus.success.ToString(),
                TransactionId = penaltyTransactionId,
                PaidAt = Common.Utils.VietnamTime.Now
            };

            await _paymentRepository.AddAsync(payment);
        }

        ticket.Status = "resolved";
        ticket.ResolvedAt = Common.Utils.VietnamTime.Now;
        ticket.ResolvedBy = confirmedBy;
        var supportNotes = $"Occupied incident penalty applied. Amount: {booking.DepositAmount:0.00}. " +
                           $"From available: {settlement.DeductedFromAvailable:0.00}, " +
                           $"from pending: {settlement.DeductedFromPending:0.00}, " +
                           $"debt: {settlement.DebtRecorded:0.00}.";
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Notes))
        {
            supportNotes += $" Staff notes: {dto.Notes}";
        }

        ticket.ResolutionNotes = string.IsNullOrWhiteSpace(ticket.ResolutionNotes)
            ? supportNotes
            : $"{ticket.ResolutionNotes}\n{supportNotes}";
        ticket.UpdatedAt = Common.Utils.VietnamTime.Now;
        _supportTicketRepository.Update(ticket);

        await _paymentRepository.SaveChangesAsync();
        await _supportTicketRepository.SaveChangesAsync();

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.system_announcement.ToString(),
            "Occupied incident penalty applied",
            $"A penalty of {booking.DepositAmount:0.00} was applied for booking {booking.BookingId} due to occupied-room incident.",
            booking.BookingId);

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.system_announcement.ToString(),
            "Occupied incident penalty confirmed",
            "Support staff confirmed your occupied-room incident and applied landlord penalty equal to the booking deposit.",
            booking.BookingId);

        // Audit event for occupied incident penalty confirmation (staff action)
        try
        {
            await CreateCheckTimeStateEventAsync(booking.BookingId, null, confirmedBy, "occupied_penalty_confirmed", new { TicketId = ticket.TicketId, PenaltyAmount = booking.DepositAmount, Notes = dto?.Notes });
        }
        catch
        {
            // swallow audit failures
        }

        return new ConfirmOccupiedIncidentPenaltyResponseDto
        {
            BookingId = booking.BookingId,
            TicketId = ticket.TicketId,
            PenaltyAmount = booking.DepositAmount,
            AlreadyApplied = existingPenalty != null,
            Message = existingPenalty != null
                ? "Occupied incident was already settled. Refund and offer handling were verified."
                : "Occupied incident penalty applied successfully. Refund processed first and an alternative offer was created afterward.",
            Settlement = settlement
        };
    }

    private async Task<bool> HasSuccessfulBookingRefundAsync(Guid bookingId)
    {
        return (await _paymentRepository.FindAsync(payment =>
            payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
            && payment.RelatedEntityId == bookingId
            && payment.PaymentType == PaymentTypes.refund.ToString()
            && payment.Status == PaymentStatus.success.ToString())).Any();
    }

    private async Task<SupportTicket?> ResolveOccupiedIncidentTicketAsync(Guid bookingId, Guid tenantId, Guid? ticketId)
    {
        bool IsOccupiedIncidentTicket(SupportTicket ticket)
        {
            var subject = ticket.Subject ?? string.Empty;
            var description = ticket.Description ?? string.Empty;
            var bookingMarker = bookingId.ToString();

            return ticket.Category != null
                && string.Equals(ticket.Category, "booking_issue", StringComparison.OrdinalIgnoreCase)
                && ticket.UserId == tenantId
                && ((ticket.BookingId.HasValue && ticket.BookingId.Value == bookingId)
                    || subject.Contains(bookingMarker, StringComparison.OrdinalIgnoreCase)
                    || description.Contains(bookingMarker, StringComparison.OrdinalIgnoreCase))
                && (subject.Contains("occupied", StringComparison.OrdinalIgnoreCase)
                    || description.Contains("occupied", StringComparison.OrdinalIgnoreCase));
        }

        if (ticketId.HasValue)
        {
            var byId = await _supportTicketRepository.GetByIdAsync(ticketId.Value);
            if (byId == null)
            {
                return null;
            }

            if (!IsOccupiedIncidentTicket(byId))
            {
                return null;
            }

            return byId;
        }

        var subjectMarker = bookingId.ToString();
        var candidates = await _supportTicketRepository.FindAsync(t =>
            t.UserId == tenantId &&
            t.Category == "booking_issue");

        return candidates
            .Where(IsOccupiedIncidentTicket)
            .OrderByDescending(t => t.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();
    }

    private async Task CreateBookingNotificationAsync(
        Guid userId,
        string type,
        string title,
        string message,
        Guid bookingId)
    {
        var notification = new DAL.Models.Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = bookingId,
            ReferenceType = "booking",
            IsRead = false,
            CreatedAt = Common.Utils.VietnamTime.Now
        };

        await _notificationRepository.AddAsync(notification);
        await _notificationRepository.SaveChangesAsync();
    }

    public override async Task<(IEnumerable<Booking> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        var effectiveAllowedColumns = new[]
        {
            "BookingId",
            "TenantId",
            "TenantName",
            "ApartmentId",
            "CheckInDate",
            "CheckOutDate",
            "Status",
            "CreatedAt"
        };

        return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
    }

    public async Task<BookingQuoteResponseDto> GetQuoteAsync(BookingQuoteRequestDto dto)
    {
        var (checkInDate, checkOutDate, _, _, nights) = ResolveBookingWindow(
            dto.CheckInDate,
            dto.CheckOutDate,
            dto.CheckInDateTime,
            dto.CheckOutDateTime);

        if (nights > MaxBookingDays)
            throw new ArgumentException($"Booking duration cannot exceed {MaxBookingDays} days.");

        var apartment = await _apartmentRepository.GetByIdAsync(dto.ApartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        if (!string.Equals(apartment.Status, "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This apartment is not currently available for booking.");

        if (string.Equals(apartment.BookingStatus, "locked", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apartment.BookingStatus, ApartmentBookingStatusEnum.Locked.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This apartment is currently locked for booking.");

        ValidateOccupancyLimits(
            apartment,
            dto.NoOfAdults,
            dto.NoOfChildren,
            dto.NoOfInfants,
            dto.NoOfPets);

        await EnsureNoConflictingBookingsAsync(dto.ApartmentId, checkInDate, checkOutDate);

        // Enforce minimum stay based on apartment price calendar rules
        var calendars = await _apartmentPriceCalendarRepository.FindAsync(c =>
            c.ApartmentId == dto.ApartmentId &&
            c.StartDate <= checkOutDate &&
            c.EndDate >= checkInDate);
        var priceCalendars = calendars.ToList();

        if (priceCalendars.Any())
        {
            var minRequiredNights = priceCalendars.Max(c => c.MinNights ?? 1);
            if (nights < minRequiredNights)
            {
                throw new InvalidOperationException($"Booking must be at least {minRequiredNights} night(s) for the selected dates.");
            }
        }

        var priceCalendar = BuildPriceCalendarBreakdown(
            checkInDate,
            checkOutDate,
            apartment.BasePricePerNight,
            priceCalendars);

        var baseAmount = CalculateBaseAmountForRange(
            checkInDate,
            checkOutDate,
            apartment.BasePricePerNight,
            priceCalendars);

        decimal packageAmount = 0m;
        if (dto.PackageId.HasValue)
        {
            var package = await _packageRepository.GetByIdAsync(dto.PackageId.Value);
            if (package == null || package.ApartmentId != dto.ApartmentId || package.IsActive == false)
                throw new ArgumentException("Selected package is invalid for this apartment.");

            packageAmount = package.Price;
        }

        var total = baseAmount + packageAmount;
        var suggestedDeposit = Math.Round(total * SuggestedDepositRate, 2, MidpointRounding.AwayFromZero);
        var remaining = total - suggestedDeposit;

        return new BookingQuoteResponseDto
        {
            ApartmentId = dto.ApartmentId,
            PackageId = dto.PackageId,
            Nights = nights,
            BasePricePerNight = apartment.BasePricePerNight,
            ResolvedPricePerNight = Math.Round(baseAmount / nights, 2, MidpointRounding.AwayFromZero),
            BaseAmount = baseAmount,
            PackageAmount = packageAmount,
            TotalPrice = total,
            SuggestedDeposit = suggestedDeposit,
            RemainingBalance = remaining,
            FullUpfrontPaymentAmount = total,
            FullUpfrontLandlordShareAmount = Math.Round(total * FullPaymentLandlordShareRate, 2, MidpointRounding.AwayFromZero),
            PriceCalendar = priceCalendar
        };
    }

    public async Task<Booking> CreateWithQuoteAsync(CreateBookingRequestDto requestDto, Guid tenantId)
    {
        await _identityVerificationService.EnsureUserVerifiedForBookingAsync(tenantId);
        await EnsureTenantHasNoOutstandingCheckTimeFeesAsync(tenantId);
        await EnsureTenantHasNoPendingCheckTimeRequestsAsync(tenantId);

        var admission = await EvaluateTenantBookingAdmissionAsync(tenantId, requestDto.PaymentMode);
        if (!admission.Allowed)
        {
            throw new BookingAdmissionPolicyException(admission);
        }

        var (checkInDate, checkOutDate, checkInDateTime, checkOutDateTime, nights) = ResolveBookingWindow(
            requestDto.CheckInDate,
            requestDto.CheckOutDate,
            requestDto.CheckInDateTime,
            requestDto.CheckOutDateTime);

        if (nights > MaxBookingDays)
            throw new ArgumentException($"Booking duration cannot exceed {MaxBookingDays} days.");

        var quote = await GetQuoteAsync(new BookingQuoteRequestDto
        {
            ApartmentId = requestDto.ApartmentId,
            PackageId = requestDto.PackageId,
            NoOfAdults = requestDto.NoOfAdults,
            NoOfChildren = requestDto.NoOfChildren,
            NoOfInfants = requestDto.NoOfInfants,
            NoOfPets = requestDto.NoOfPets,
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            CheckInDateTime = checkInDateTime,
            CheckOutDateTime = checkOutDateTime
        });

        var paymentMode = requestDto.PaymentMode;
        var upfrontPaymentAmount = paymentMode == BookingPaymentMode.full
            ? quote.TotalPrice
            : quote.SuggestedDeposit;

        var depositAmount = quote.SuggestedDeposit;
        if (depositAmount > quote.TotalPrice)
            throw new ArgumentException("Deposit cannot exceed total booking price.");

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            TenantId = tenantId,
            ApartmentId = requestDto.ApartmentId,
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            Nights = quote.Nights,
            NoOfAdults = requestDto.NoOfAdults,
            NoOfChildren = requestDto.NoOfChildren,
            NoOfInfants = requestDto.NoOfInfants,
            NoOfPets = requestDto.NoOfPets,
            TotalPrice = quote.TotalPrice,
            PackageId = requestDto.PackageId,
            PackagePrice = quote.PackageAmount,
            DepositAmount = depositAmount,
            UpfrontPaymentAmount = upfrontPaymentAmount,
            DepositPaid = false,
            AmountPaid = 0m,
            RemainingAmount = quote.TotalPrice,
            PaymentMode = paymentMode.ToString(),
            BalanceDueDate = checkInDate.AddDays(-1),
            Status = "pending",
            CreatedAt = Common.Utils.VietnamTime.Now
        };

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        var checkTime = new BookingCheckTime
        {
            CheckTimeId = Guid.NewGuid(),
            BookingId = booking.BookingId,
            ScheduledCheckIn = checkInDateTime,
            ScheduledCheckOut = checkOutDateTime,
            FeeSettlementStatus = FeeSettlementStatusNone,
            TempResidenceReported = false,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };
        await _bookingCheckTimeRepository.AddAsync(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            await CreateBookingNotificationAsync(
                apartment.LandlordId,
                NotificationType.booking_created.ToString(),
                "New booking request",
                $"A new booking has been created for your apartment '{apartment.Title}'.",
                booking.BookingId);

            await CreateBookingNotificationAsync(
                booking.TenantId,
                NotificationType.booking_created.ToString(),
                "Booking created",
                $"Your booking for apartment '{apartment.Title}' has been created.",
                booking.BookingId);
        }

        return booking;
    }

    private static (DateOnly CheckInDate, DateOnly CheckOutDate, DateTime CheckInDateTime, DateTime CheckOutDateTime, int Nights)
        ResolveBookingWindow(
            DateOnly? checkInDate,
            DateOnly? checkOutDate,
            DateTime? checkInDateTime,
            DateTime? checkOutDateTime)
    {
        if (checkInDateTime.HasValue || checkOutDateTime.HasValue)
        {
            if (!checkInDateTime.HasValue || !checkOutDateTime.HasValue)
                throw new ArgumentException("Both check-in and check-out date-time are required.");

            if (checkInDateTime.Value >= checkOutDateTime.Value)
                throw new ArgumentException("Check-out date-time must be later than check-in date-time.");

            var resolvedCheckInDate = DateOnly.FromDateTime(checkInDateTime.Value);
            var resolvedCheckOutDate = DateOnly.FromDateTime(checkOutDateTime.Value);
            var nights = (int)Math.Ceiling((checkOutDateTime.Value - checkInDateTime.Value).TotalDays);
            nights = Math.Max(1, nights);

            return (resolvedCheckInDate, resolvedCheckOutDate, checkInDateTime.Value, checkOutDateTime.Value, nights);
        }

        if (!checkInDate.HasValue || !checkOutDate.HasValue)
            throw new ArgumentException("Both check-in and check-out dates are required.");

        if (checkInDate.Value >= checkOutDate.Value)
            throw new ArgumentException("Check-out date must be later than check-in date.");

        var dateNights = checkOutDate.Value.DayNumber - checkInDate.Value.DayNumber;
        return (
            checkInDate.Value,
            checkOutDate.Value,
            checkInDate.Value.ToDateTime(new TimeOnly(14, 0)),
            checkOutDate.Value.ToDateTime(new TimeOnly(12, 0)),
            dateNights);
    }

    private static void ValidateOccupancyLimits(
        Apartment apartment,
        int? requestedAdults,
        int? requestedChildren,
        int? requestedInfants,
        int? requestedPets)
    {
        var adults = requestedAdults ?? 0;
        var children = requestedChildren ?? 0;
        var infants = requestedInfants ?? 0;

        if (apartment.MaxOccupants.HasValue && adults > apartment.MaxOccupants.Value)
        {
            throw new InvalidOperationException($"This apartment allows at most {apartment.MaxOccupants.Value} adult(s).");
        }

        if (apartment.MaxOccupants.HasValue && children > apartment.MaxOccupants.Value)
        {
            throw new InvalidOperationException($"This apartment allows at most {apartment.MaxOccupants.Value} child(ren).");
        }

        if (apartment.MaxOccupants.HasValue && (adults + children) > apartment.MaxOccupants.Value)
        {
            throw new InvalidOperationException($"This apartment allows at most {apartment.MaxOccupants.Value} occupant(s) (adults + children).");
        }

        if (apartment.MaxInfants.HasValue && infants > apartment.MaxInfants.Value)
        {
            throw new InvalidOperationException($"This apartment allows at most {apartment.MaxInfants.Value} infant(s).");
        }

        var pets = requestedPets ?? 0;
        if (apartment.IsPetAllowed != true && pets > 0)
        {
            throw new InvalidOperationException("This apartment does not allow pets.");
        }

        if (apartment.IsPetAllowed == true && apartment.MaxPets.HasValue && pets > apartment.MaxPets.Value)
        {
            throw new InvalidOperationException($"This apartment allows at most {apartment.MaxPets.Value} pet(s).");
        }
    }

    private async Task<BookingCheckTime> GetOrCreateBookingCheckTimeAsync(Booking booking)
    {
        // Reuse the navigation instance when available to avoid tracking two
        // BookingCheckTime objects with the same key in one DbContext scope.
        if (booking.BookingCheckTime != null)
        {
            return booking.BookingCheckTime;
        }

        var checkTime = (await _bookingCheckTimeRepository.FindAsync(ct => ct.BookingId == booking.BookingId)).FirstOrDefault();
        if (checkTime != null)
        {
            booking.BookingCheckTime = checkTime;
            return checkTime;
        }

        var now = Common.Utils.VietnamTime.Now;
        checkTime = new BookingCheckTime
        {
            CheckTimeId = Guid.NewGuid(),
            BookingId = booking.BookingId,
            ScheduledCheckIn = booking.CheckInDate.ToDateTime(new TimeOnly(14, 0)),
            ScheduledCheckOut = booking.CheckOutDate.ToDateTime(new TimeOnly(12, 0)),
            TempResidenceReported = false,
            TenantResponseStatus = "pending",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _bookingCheckTimeRepository.AddAsync(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();
        booking.BookingCheckTime = checkTime;
        return checkTime;
    }

    public async Task<Booking> MarkDepositPaidAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        var apartmentLock = DepositConfirmationLocks.GetOrAdd(booking.ApartmentId, _ => new SemaphoreSlim(1, 1));
        await apartmentLock.WaitAsync();

        try
        {
            booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null)
                throw new ArgumentException("Booking not found.");

            if (booking.DepositPaid == true)
            {
                var currentPaymentMode = GetBookingPaymentMode(booking);
                var shouldSetPaid = currentPaymentMode == BookingPaymentMode.full
                    && !string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase);
                var shouldSetConfirmed = currentPaymentMode == BookingPaymentMode.partial
                    && (string.Equals(booking.Status, "pending", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(booking.Status, "negotiating", StringComparison.OrdinalIgnoreCase));

                if (shouldSetPaid)
                {
                    booking.Status = "paid";
                    _bookingRepository.Update(booking);
                    await _bookingRepository.SaveChangesAsync();
                    await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);
                }
                else if (shouldSetConfirmed)
                {
                    booking.Status = "confirmed";
                    _bookingRepository.Update(booking);
                    await _bookingRepository.SaveChangesAsync();
                    await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);
                }

                // Recalculate paid amounts from payment ledger for consistency
                var paidPaymentsEarly = (await _paymentRepository.FindAsync(p =>
                    p.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                    && p.RelatedEntityId == booking.BookingId
                    && p.Status == PaymentStatus.success.ToString()
                    && p.PaymentType != PaymentTypes.refund.ToString())).ToList();

                var totalPaidEarly = paidPaymentsEarly.Sum(p => p.Amount);
                booking.AmountPaid = Math.Round(totalPaidEarly, 2, MidpointRounding.AwayFromZero);
                booking.RemainingAmount = Math.Max(0m, Math.Round(booking.TotalPrice - booking.AmountPaid, 2, MidpointRounding.AwayFromZero));

                _bookingRepository.Update(booking);
                await _bookingRepository.SaveChangesAsync();

                return booking;
            }

            await _identityVerificationService.EnsureUserVerifiedForBookingAsync(booking.TenantId);

            var paymentMode = GetBookingPaymentMode(booking);
            var winningStatuses = new[] { "confirmed", "paid", "completed", "disputed" };
            var existingWinner = (await _bookingRepository.FindAsync(b =>
                b.BookingId != booking.BookingId &&
                b.ApartmentId == booking.ApartmentId &&
                b.Status != null &&
                winningStatuses.Contains(b.Status) &&
                b.CheckInDate < booking.CheckOutDate &&
                b.CheckOutDate > booking.CheckInDate)).Any();

            var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);

            booking.DepositPaid = true;
            if (existingWinner)
            {
                await RefundBookingAsync(
                    booking.BookingId,
                    booking.TenantId,
                    new RequestBookingRefundDto
                    {
                        Reason = "system_cancellation",
                        Notes = apartment != null
                            ? $"Cancelled because another booking for apartment '{apartment.Title}' was already confirmed."
                            : "Cancelled because another booking for the same dates was already confirmed."
                    });

                return booking;
            }

            if (paymentMode == BookingPaymentMode.full)
            {
                booking.Status = "paid";
            }
            else if (string.Equals(booking.Status, "pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(booking.Status, "negotiating", StringComparison.OrdinalIgnoreCase))
            {
                booking.Status = "confirmed";
            }

            // Recalculate paid amounts from payment ledger
            var paidPayments = (await _paymentRepository.FindAsync(p =>
                p.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                && p.RelatedEntityId == booking.BookingId
                && p.Status == PaymentStatus.success.ToString()
                && p.PaymentType != PaymentTypes.refund.ToString())).ToList();

            var totalPaid = paidPayments.Sum(p => p.Amount);
            booking.AmountPaid = Math.Round(totalPaid, 2, MidpointRounding.AwayFromZero);
            booking.RemainingAmount = Math.Max(0m, Math.Round(booking.TotalPrice - booking.AmountPaid, 2, MidpointRounding.AwayFromZero));

            _bookingRepository.Update(booking);
            await _bookingRepository.SaveChangesAsync();
            await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

            if (apartment != null)
            {
                var paymentDescriptor = paymentMode == BookingPaymentMode.full ? "full payment" : "partial payment";

                await CreateBookingNotificationAsync(
                    apartment.LandlordId,
                    NotificationType.booking_confirmed.ToString(),
                    "New booking confirmed",
                    $"A booking for apartment '{apartment.Title}' has been confirmed with {paymentDescriptor}. Payment will be credited to your wallet after guest checkout.",
                    booking.BookingId);

                await CreateBookingNotificationAsync(
                    booking.TenantId,
                    NotificationType.booking_confirmed.ToString(),
                    "Booking confirmed",
                    $"Your booking for apartment '{apartment.Title}' has been confirmed after {paymentDescriptor}.",
                    booking.BookingId);
            }

            return booking;
        }
        finally
        {
            apartmentLock.Release();
        }
    }

    public async Task<Booking> MarkBalancePaidAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        var paymentMode = GetBookingPaymentMode(booking);
        if (paymentMode == BookingPaymentMode.full)
            throw new InvalidOperationException("This booking was paid in full upfront and has no remaining balance.");

        if (string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase))
            return booking;

        if (booking.DepositPaid != true)
            throw new InvalidOperationException("Deposit must be paid before settling remaining balance.");

        booking.Status = "paid";
        // Recalculate paid amounts from payment ledger
        var paidPayments = (await _paymentRepository.FindAsync(p =>
            p.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
            && p.RelatedEntityId == booking.BookingId
            && p.Status == PaymentStatus.success.ToString()
            && p.PaymentType != PaymentTypes.refund.ToString())).ToList();

        var totalPaid = paidPayments.Sum(p => p.Amount);
        booking.AmountPaid = Math.Round(totalPaid, 2, MidpointRounding.AwayFromZero);
        booking.RemainingAmount = Math.Max(0m, Math.Round(booking.TotalPrice - booking.AmountPaid, 2, MidpointRounding.AwayFromZero));

        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            await CreateBookingNotificationAsync(
                booking.TenantId,
                NotificationType.payment_success.ToString(),
                "Booking payment completed",
                $"Your payment for booking at '{apartment.Title}' is complete.",
                booking.BookingId);

            await CreateBookingNotificationAsync(
                apartment.LandlordId,
                NotificationType.payment_success.ToString(),
                "Booking payment received",
                $"Payment for booking at '{apartment.Title}' has been completed. Funds will be credited to your wallet after guest checkout.",
                booking.BookingId);
        }

        return booking;
    }

    public async Task<BookingRefundResponseDto> RefundBookingViaPayOsAsync(Guid bookingId, Guid requesterId, RequestBookingRefundDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        var normalizedReason = dto.Reason.Trim().ToLowerInvariant();
        var isSystemCancellation = string.Equals(normalizedReason, "system_cancellation", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This booking is no longer eligible for refund.");
        }

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        var now = Common.Utils.VietnamTime.Now;
        var hoursUntilCheckIn = (checkTime.ScheduledCheckIn - now).TotalHours;

        if (!isSystemCancellation && hoursUntilCheckIn < 24)
        {
            throw new InvalidOperationException("Refunds are only allowed at least 24 hours before check-in.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        var refundedPayments = (await _paymentRepository.FindAsync(payment =>
                payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                && payment.RelatedEntityId == booking.BookingId
                && payment.Status == PaymentStatus.success.ToString()
                && payment.PaymentType != PaymentTypes.refund.ToString()))
            .ToList();

        var existingRefund = (await _paymentRepository.FindAsync(payment =>
                payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                && payment.RelatedEntityId == booking.BookingId
                && payment.PaymentType == PaymentTypes.refund.ToString()
                && payment.Status == PaymentStatus.success.ToString()))
            .Any();

        if (existingRefund)
        {
            throw new InvalidOperationException("A refund has already been processed for this booking.");
        }

        var totalPaidAmount = refundedPayments.Sum(payment => payment.Amount);
        var processingFeeAmount = Math.Round(totalPaidAmount * 0.02m, 2, MidpointRounding.AwayFromZero);
        var netRefundAmount = Math.Max(0m, totalPaidAmount - processingFeeAmount);

        if (refundedPayments.Count == 0)
        {
            booking.Status = "cancelled";
            _bookingRepository.Update(booking);
            await _bookingRepository.SaveChangesAsync();
            await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

            return new BookingRefundResponseDto
            {
                BookingId = booking.BookingId,
                Status = booking.Status ?? "cancelled",
                TotalPaidAmount = 0m,
                ProcessingFeeAmount = 0m,
                NetRefundAmount = 0m,
                RefundedPaymentCount = 0,
                ProcessedAt = now,
                Message = "Booking cancelled. No paid amount was found to refund."
            };
        }

        // Build reference and call PayOS payout
        var reference = $"RF{booking.BookingId:N}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // Use DTO provided PayOS destination (controller enforces present)
        var receiverName = dto.PayOsReceiverName!.Trim();
        var accountNumber = dto.PayOsAccountNumber!.Trim();
        var bankCode = dto.PayOsBankCode!.Trim();

        // PayOSPayoutService expects integer amount (currency units). Round to nearest integer.
        var payoutAmountLong = Convert.ToInt64(Math.Round(netRefundAmount, 0, MidpointRounding.AwayFromZero));

        PayOSPayoutResult payosResult;
        try
        {
            payosResult = await _payOSPayoutService.CreateBankPayoutAsync(receiverName, accountNumber, bankCode, payoutAmountLong, reference);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PayOS payout request failed.", ex);
        }

        if (payosResult == null)
        {
            throw new InvalidOperationException("PayOS payout returned no result.");
        }

        // If PayOS reports non-successful result, treat it as failure (you may accept pending codes per business rule)
        if (payosResult.ResultCode != 0)
        {
            throw new InvalidOperationException($"PayOS payout failed: {payosResult.Message ?? "unknown"} (code {payosResult.ResultCode})");
        }

        // Create a single refund payment record for the payout
        var refundPayment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            Amount = netRefundAmount,
            PaymentType = PaymentTypes.refund.ToString(),
            PaymentPurpose = PaymentPurposes.refund_booking.ToString(),
            RelatedEntityId = booking.BookingId,
            RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
            LandlordId = apartment.LandlordId,
            LandlordAmount = 0m,
            PlatformFee = processingFeeAmount,
            SettlementStatus = payosResult.ResultCode == 0 ? "paid" : "pending",
            Method = "payos_bank",
            Status = PaymentStatus.success.ToString(),
            TransactionId = payosResult.PayoutId ?? payosResult.TransId,
            PaidAt = now
        };

        await _paymentRepository.AddAsync(refundPayment);

        // Mark original payments as refunded and rollback landlord pending amounts proportionally (preserve prior behavior)
        foreach (var payment in refundedPayments)
        {
            payment.Status = PaymentStatus.refunded.ToString();
            _paymentRepository.Update(payment);

            var landlordShare = Math.Round(payment.Amount * FullPaymentLandlordShareRate, 2, MidpointRounding.AwayFromZero);
            await _landlordWalletService.RollbackPendingAsync(apartment.LandlordId, landlordShare);
        }

        booking.Status = "cancelled";
        _bookingRepository.Update(booking);

        await _paymentRepository.SaveChangesAsync();
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.booking_cancelled.ToString(),
            "Booking refunded",
            $"Your booking refund has been processed via PayOS. Net amount refunded: {netRefundAmount:0.00}. Processing fee: {processingFeeAmount:0.00}.",
            booking.BookingId);

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.booking_cancelled.ToString(),
            "Booking refunded",
            $"Booking {booking.BookingId} was refunded to the tenant via PayOS.",
            booking.BookingId);

        return new BookingRefundResponseDto
        {
            BookingId = booking.BookingId,
            Status = booking.Status ?? "cancelled",
            TotalPaidAmount = totalPaidAmount,
            ProcessingFeeAmount = processingFeeAmount,
            NetRefundAmount = netRefundAmount,
            RefundedPaymentCount = refundedPayments.Count,
            ProcessedAt = now,
            Message = string.IsNullOrWhiteSpace(dto.Notes)
                ? "Booking refund processed successfully via PayOS."
                : $"Booking refund processed successfully via PayOS. Notes: {dto.Notes}"
        };
    }

    public async Task<BookingRefundResponseDto> RefundBookingAsync(Guid bookingId, Guid requesterId, RequestBookingRefundDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        var normalizedReason = dto.Reason.Trim().ToLowerInvariant();
        var isSystemCancellation = string.Equals(normalizedReason, "system_cancellation", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This booking is no longer eligible for refund.");
        }

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        var now = Common.Utils.VietnamTime.Now;
        var hoursUntilCheckIn = (checkTime.ScheduledCheckIn - now).TotalHours;

        if (!isSystemCancellation && hoursUntilCheckIn < 24)
        {
            throw new InvalidOperationException("Refunds are only allowed at least 24 hours before check-in.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        var refundedPayments = (await _paymentRepository.FindAsync(payment =>
                payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                && payment.RelatedEntityId == booking.BookingId
                && payment.Status == PaymentStatus.success.ToString()
                && payment.PaymentType != PaymentTypes.refund.ToString()))
            .ToList();

        var existingRefund = (await _paymentRepository.FindAsync(payment =>
                payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString()
                && payment.RelatedEntityId == booking.BookingId
                && payment.PaymentType == PaymentTypes.refund.ToString()
                && payment.Status == PaymentStatus.success.ToString()))
            .Any();

        if (existingRefund)
        {
            throw new InvalidOperationException("A refund has already been processed for this booking.");
        }

        var totalPaidAmount = refundedPayments.Sum(payment => payment.Amount);
        var processingFeeAmount = Math.Round(totalPaidAmount * 0.02m, 2, MidpointRounding.AwayFromZero);
        var netRefundAmount = Math.Max(0m, totalPaidAmount - processingFeeAmount);

        if (refundedPayments.Count == 0)
        {
            booking.Status = "cancelled";
            _bookingRepository.Update(booking);
            await _bookingRepository.SaveChangesAsync();
            await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

            return new BookingRefundResponseDto
            {
                BookingId = booking.BookingId,
                Status = booking.Status ?? "cancelled",
                TotalPaidAmount = 0m,
                ProcessingFeeAmount = 0m,
                NetRefundAmount = 0m,
                RefundedPaymentCount = 0,
                ProcessedAt = now,
                Message = "Booking cancelled. No paid amount was found to refund."
            };
        }

        var refundAllocations = AllocateRefundAmounts(refundedPayments.Select(payment => payment.Amount).ToList(), netRefundAmount);

        for (var index = 0; index < refundedPayments.Count; index++)
        {
            var payment = refundedPayments[index];
            var refundAmount = refundAllocations[index];

            if (string.Equals(payment.Method, "stripe", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(payment.TransactionId))
                {
                    throw new InvalidOperationException($"Stripe payment {payment.PaymentId} is missing a transaction ID.");
                }

                var refundId = await _stripeService.RefundCheckoutSessionAsync(
                    payment.TransactionId,
                    Convert.ToInt64(Math.Round(refundAmount, 0, MidpointRounding.AwayFromZero)));

                var refundPayment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = refundAmount,
                    PaymentType = PaymentTypes.refund.ToString(),
                    PaymentPurpose = PaymentPurposes.refund_booking.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    LandlordId = apartment.LandlordId,
                    LandlordAmount = 0m,
                    PlatformFee = 0m,
                    SettlementStatus = "pending",
                    Method = payment.Method,
                    Status = PaymentStatus.success.ToString(),
                    TransactionId = refundId,
                    PaidAt = now
                };

                await _paymentRepository.AddAsync(refundPayment);
            }
            else if (string.Equals(payment.Method, "momo_wallet", StringComparison.OrdinalIgnoreCase))
            {
                var originalTransId = await ResolveMomoPaymentTransIdAsync(payment);
                var refundRequest = new MomoRefundPaymentRequest
                {
                    OrderId = $"RF{booking.BookingId:N}{index + 1}",
                    RequestId = $"RF{Guid.NewGuid():N}"[..22],
                    Amount = Convert.ToInt64(Math.Round(refundAmount, 0, MidpointRounding.AwayFromZero)),
                    TransId = originalTransId,
                    Lang = "vi",
                    Description = dto.Notes ?? $"Refund for booking {booking.BookingId}"
                };

                var refundResult = await _momoService.RefundPaymentAsync(refundRequest);
                if (refundResult.ResultCode != 0)
                {
                    throw new InvalidOperationException($"MoMo refund failed: {refundResult.Message}");
                }

                var refundPayment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = refundAmount,
                    PaymentType = PaymentTypes.refund.ToString(),
                    PaymentPurpose = PaymentPurposes.refund_booking.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    LandlordId = apartment.LandlordId,
                    LandlordAmount = 0m,
                    PlatformFee = 0m,
                    SettlementStatus = "pending",
                    Method = payment.Method,
                    Status = PaymentStatus.success.ToString(),
                    TransactionId = refundResult.TransId.ToString(),
                    PaidAt = now
                };

                await _paymentRepository.AddAsync(refundPayment);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Refunds for payment method '{payment.Method}' are not supported by this flow. Use the PayOS refund endpoint with payout details or refund a Stripe/MoMo payment.");
            }

            payment.Status = PaymentStatus.refunded.ToString();
            _paymentRepository.Update(payment);

            var landlordShare = Math.Round(payment.Amount * FullPaymentLandlordShareRate, 2, MidpointRounding.AwayFromZero);
            await _landlordWalletService.RollbackPendingAsync(apartment.LandlordId, landlordShare);
        }

        booking.Status = "cancelled";
        _bookingRepository.Update(booking);

        await _paymentRepository.SaveChangesAsync();
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.booking_cancelled.ToString(),
            "Booking refunded",
            $"Your booking refund has been processed. Net amount refunded: {netRefundAmount:0.00}. Processing fee: {processingFeeAmount:0.00}.",
            booking.BookingId);

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.booking_cancelled.ToString(),
            "Booking refunded",
            $"Booking {booking.BookingId} was refunded to the tenant.",
            booking.BookingId);

        return new BookingRefundResponseDto
        {
            BookingId = booking.BookingId,
            Status = booking.Status ?? "cancelled",
            TotalPaidAmount = totalPaidAmount,
            ProcessingFeeAmount = processingFeeAmount,
            NetRefundAmount = netRefundAmount,
            RefundedPaymentCount = refundedPayments.Count,
            ProcessedAt = now,
            Message = string.IsNullOrWhiteSpace(dto.Notes)
                ? "Booking refund processed successfully."
                : $"Booking refund processed successfully. Notes: {dto.Notes}"
        };
    }

    private static List<decimal> AllocateRefundAmounts(IReadOnlyList<decimal> paymentAmounts, decimal totalRefundAmount)
    {
        if (paymentAmounts.Count == 0)
        {
            return new List<decimal>();
        }

        if (paymentAmounts.Count == 1)
        {
            return new List<decimal> { totalRefundAmount };
        }

        var allocations = new List<decimal>(paymentAmounts.Count);
        var remainingRefund = totalRefundAmount;
        var remainingBase = paymentAmounts.Sum();

        for (var index = 0; index < paymentAmounts.Count; index++)
        {
            var amount = paymentAmounts[index];
            var allocation = index == paymentAmounts.Count - 1
                ? remainingRefund
                : Math.Round((amount / remainingBase) * totalRefundAmount, 2, MidpointRounding.AwayFromZero);

            allocations.Add(allocation);
            remainingRefund -= allocation;
            remainingBase -= amount;
        }

        return allocations;
    }

    private async Task<long> ResolveMomoPaymentTransIdAsync(Payment payment)
    {
        if (long.TryParse(payment.TransactionId, out var existingTransId) && existingTransId > 0)
        {
            return existingTransId;
        }

        if (string.IsNullOrWhiteSpace(payment.TransactionId))
        {
            throw new InvalidOperationException($"MoMo payment {payment.PaymentId} is missing an order ID.");
        }

        var queryResult = await _momoService.QueryPaymentStatusAsync(new MomoQueryPaymentRequest
        {
            OrderId = payment.TransactionId,
            Lang = "vi"
        });

        if (queryResult.ResultCode != 0)
        {
            throw new InvalidOperationException($"MoMo payment status query failed: {queryResult.Message}");
        }

        if (!long.TryParse(queryResult.TransId, out var queriedTransId) || queriedTransId <= 0)
        {
            throw new InvalidOperationException("MoMo payment transaction ID was not available for refund.");
        }

        return queriedTransId;
    }

    private static BookingPaymentMode GetBookingPaymentMode(Booking booking)
    {
        return string.Equals(booking.PaymentMode, BookingPaymentMode.full.ToString(), StringComparison.OrdinalIgnoreCase)
            ? BookingPaymentMode.full
            : BookingPaymentMode.partial;
    }

    private static decimal GetUpfrontPaymentAmount(Booking booking)
    {
        return booking.UpfrontPaymentAmount > 0 ? booking.UpfrontPaymentAmount : booking.DepositAmount;
    }

    public async Task<(IEnumerable<Booking> Items, int TotalCount)> GetLandlordBookingHistoryAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var effectiveAllowedColumns = new[]
        {
            "BookingId",
            "TenantId",
            "TenantFullName",
            "ApartmentId",
            "CheckInDate",
            "CheckOutDate",
            "Status",
            "CreatedAt"
        };
        return await _bookingRepository.GetByLandlordAsync(landlordId, page, pageSize, sortBy, sortOrder, search, fromDate, toDate, effectiveAllowedColumns);
    }

    public async Task<TemporaryResidenceReport> SubmitResidenceReportAsync(Guid bookingId, Guid landlordUserId, SubmitResidenceReportDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found for this booking.");

        if (apartment.LandlordId != landlordUserId)
            throw new InvalidOperationException("Only the apartment landlord can submit residence reports.");

        var tenant = await _tenantRepository.GetByIdAsync(booking.TenantId);
        var tenantUser = await _userRepository.GetByIdAsync(booking.TenantId);
        if (tenant == null || tenantUser == null || string.IsNullOrWhiteSpace(tenantUser.Nationality))
            throw new InvalidOperationException("Tenant nationality is required before residence reporting.");

        var isVietnamese = string.Equals(tenantUser.Nationality.Trim(), "VN", StringComparison.OrdinalIgnoreCase);
        if (isVietnamese && string.IsNullOrWhiteSpace(tenantUser.NationalIdCardNumber))
            throw new InvalidOperationException("Tenant national ID card number is required for Vietnamese nationality.");

        if (!isVietnamese && string.IsNullOrWhiteSpace(tenant.PassportId))
            throw new InvalidOperationException("Tenant passport is required for non-Vietnamese nationality.");

        var tenantIdentityDocumentNumber = isVietnamese
            ? tenantUser.NationalIdCardNumber!
            : tenant.PassportId!;

        var existingReport = (await _temporaryResidenceReportRepository.FindAsync(r => r.BookingId == bookingId)).FirstOrDefault();

        if (existingReport != null)
            throw new InvalidOperationException("Residence report has already been submitted for this booking.");

        var report = new TemporaryResidenceReport
        {
            ReportId = Guid.NewGuid(),
            BookingId = bookingId,
            LandlordId = landlordUserId,
            TenantPassportId = tenantIdentityDocumentNumber,
            TenantNationality = tenantUser.Nationality!,
            CheckInDate = booking.CheckInDate,
            ReportedToPolice = dto.ReportedToPolice,
            ReportDate = dto.ReportDate ?? DateOnly.FromDateTime(Common.Utils.VietnamTime.Now),
            ReportNumber = dto.ReportNumber
        };

        await _temporaryResidenceReportRepository.AddAsync(report);
        await _temporaryResidenceReportRepository.SaveChangesAsync();

        var checkTime = (await _bookingCheckTimeRepository.FindAsync(c => c.BookingId == bookingId)).FirstOrDefault();
        if (checkTime != null)
        {
            checkTime.TempResidenceReported = true;
            checkTime.ReportedAt = Common.Utils.VietnamTime.Now;
            checkTime.ReportReference = dto.ReportNumber;
            checkTime.RecordedBy = landlordUserId;
            checkTime.RecordedAt = Common.Utils.VietnamTime.Now;
            checkTime.UpdatedAt = Common.Utils.VietnamTime.Now;
            checkTime.ActualCheckIn ??= dto.ActualCheckIn ?? Common.Utils.VietnamTime.Now;

            _bookingCheckTimeRepository.Update(checkTime);
            await _bookingCheckTimeRepository.SaveChangesAsync();
        }

        return report;
    }

    public async Task<TemporaryResidenceReportDetailsDto> GetResidenceReportDetailsAsync(Guid bookingId, Guid requesterUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
        {
            throw new ArgumentException("Booking not found.");
        }

        var isConfirmed = string.Equals(booking.Status, "confirmed", StringComparison.OrdinalIgnoreCase);
        var isPaid = string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase);

        if (!(isConfirmed || isPaid))
        {
            throw new InvalidOperationException("Booking must be in confirmed status to generate the residence report PDF.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
        {
            throw new ArgumentException("Apartment not found for this booking.");
        }

        if (apartment.LandlordId != requesterUserId)
        {
            throw new InvalidOperationException("You are not allowed to view this residence report.");
        }

        var report = (await _temporaryResidenceReportRepository.FindAsync(r => r.BookingId == bookingId)).FirstOrDefault();
        if (report == null)
        {
            throw new InvalidOperationException("Residence report has not been submitted for this booking.");
        }

        var tenantUser = await _userRepository.GetByIdAsync(booking.TenantId);
        var landlordUser = await _userRepository.GetByIdAsync(apartment.LandlordId);
        var occupants = await BuildReportOccupantsAsync(booking, report, tenantUser);
        var primaryOccupant = occupants.First();
        var occupantCount = occupants.Count;

        return new TemporaryResidenceReportDetailsDto
        {
            ReportId = report.ReportId,
            BookingId = report.BookingId,
            LandlordId = report.LandlordId,
            TenantId = booking.TenantId,
            TenantFullName = primaryOccupant.FullName,
            TenantPassportId = primaryOccupant.PassportId ?? report.TenantPassportId,
            TenantDateOfBirth = tenantUser?.Birthday,
            TenantNationalIdCardNumber = primaryOccupant.NationalIdCardNumber,
            TenantNationality = primaryOccupant.Nationality ?? report.TenantNationality,
            TenantPhone = primaryOccupant.Phone,
            TenantEmail = primaryOccupant.Email,
            TenantSex = primaryOccupant.Sex,
            LandlordFullName = landlordUser?.FullName,
            LandlordNationalIdCardNumber = landlordUser?.NationalIdCardNumber,
            LandlordPhone = landlordUser?.Phone,
            ApartmentTitle = apartment.Title,
            ApartmentAddress = apartment.Address,
            ApartmentDistrict = apartment.District,
            ApartmentCity = apartment.City,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            ReportedToPolice = report.ReportedToPolice,
            ReportDate = report.ReportDate,
            ReportNumber = report.ReportNumber,
            OccupantCount = occupantCount,
            Occupants = occupants
        };
    }

    public async Task<IReadOnlyList<ResidenceReportOccupantDto>> GetOccupantsAsync(Guid bookingId, Guid tenantUserId)
    {
        var booking = await EnsureBookingOwnedByTenantOrLandlordAsync(bookingId, tenantUserId);
        var report = (await _temporaryResidenceReportRepository.FindAsync(r => r.BookingId == bookingId)).FirstOrDefault();
        var tenantUser = await _userRepository.GetByIdAsync(booking.TenantId);
        return await BuildReportOccupantsAsync(booking, report, tenantUser);
    }

    public async Task<ResidenceReportOccupantDto> AddOccupantAsync(Guid bookingId, Guid tenantUserId, AddBookingOccupantDto dto)
    {
        if (_bookingOccupantRepository == null)
        {
            throw new InvalidOperationException("Booking occupant persistence is not configured.");
        }

        var booking = await EnsureBookingOwnedByTenantOrLandlordAsync(bookingId, tenantUserId);
        var occupantCap = ResolveBookingOccupantCap(booking);

        var existing = (await _bookingOccupantRepository.FindAsync(o => o.BookingId == bookingId)).ToList();
        if (existing.Count >= occupantCap)
        {
            throw new InvalidOperationException($"This booking allows at most {occupantCap} occupant(s) based on the requested headcount.");
        }

        EnsureNoDuplicateOccupantIdentity(
            existing,
            dto.PassportId,
            dto.NationalIdCardNumber,
            dto.FullName,
            dto.DateOfBirth);

        var nextOrder = existing.Count == 0
            ? 1
            : existing.Max(o => o.OccupantOrder) + 1;

        var entity = new BookingOccupant
        {
            OccupantId = Guid.NewGuid(),
            BookingId = bookingId,
            OccupantOrder = nextOrder,
            IsPrimary = !existing.Any(),
            FullName = dto.FullName,
            PassportId = dto.PassportId,
            DateOfBirth = dto.DateOfBirth,
            NationalIdCardNumber = dto.NationalIdCardNumber,
            Nationality = dto.Nationality,
            Sex = dto.Sex,
            Phone = dto.Phone,
            Email = dto.Email,
            ProofPhotoUrl = dto.ProofPhotoUrl,
            CreatedAt = Common.Utils.VietnamTime.Now
        };

        await _bookingOccupantRepository.AddAsync(entity);
        await _bookingOccupantRepository.SaveChangesAsync();

        return MapBookingOccupant(entity);
    }

    public async Task<ResidenceReportOccupantDto> UpdateOccupantAsync(Guid bookingId, Guid tenantUserId, int occupantOrder, UpdateBookingOccupantDto dto)
    {
        if (_bookingOccupantRepository == null)
        {
            throw new InvalidOperationException("Booking occupant persistence is not configured.");
        }

        _ = await EnsureBookingOwnedByTenantOrLandlordAsync(bookingId, tenantUserId);

        var existing = (await _bookingOccupantRepository.FindAsync(o => o.BookingId == bookingId)).ToList();
        var occupant = existing.FirstOrDefault(o => o.OccupantOrder == occupantOrder)
            ?? throw new ArgumentException("Occupant not found for this booking.");

        var nextPassportId = dto.PassportId ?? occupant.PassportId;
        var nextNationalIdCardNumber = dto.NationalIdCardNumber ?? occupant.NationalIdCardNumber;
        var nextFullName = dto.FullName ?? occupant.FullName;
        var nextDateOfBirth = dto.DateOfBirth ?? occupant.DateOfBirth;

        EnsureNoDuplicateOccupantIdentity(
            existing,
            nextPassportId,
            nextNationalIdCardNumber,
            nextFullName,
            nextDateOfBirth,
            occupant.OccupantId);

        if (dto.IsPrimary == true)
        {
            foreach (var item in existing.Where(e => e.IsPrimary && e.OccupantId != occupant.OccupantId))
            {
                item.IsPrimary = false;
                _bookingOccupantRepository.Update(item);
            }

            occupant.IsPrimary = true;
        }
        else if (dto.IsPrimary == false)
        {
            var anotherPrimaryExists = existing.Any(e => e.OccupantId != occupant.OccupantId && e.IsPrimary);
            if (!anotherPrimaryExists)
            {
                throw new InvalidOperationException("Booking must have at least one primary occupant.");
            }

            occupant.IsPrimary = false;
        }

        occupant.FullName = dto.FullName ?? occupant.FullName;
        occupant.PassportId = dto.PassportId ?? occupant.PassportId;
        occupant.DateOfBirth = dto.DateOfBirth ?? occupant.DateOfBirth;
        occupant.NationalIdCardNumber = dto.NationalIdCardNumber ?? occupant.NationalIdCardNumber;
        occupant.Nationality = dto.Nationality ?? occupant.Nationality;
        occupant.Sex = dto.Sex ?? occupant.Sex;
        occupant.Phone = dto.Phone ?? occupant.Phone;
        occupant.Email = dto.Email ?? occupant.Email;
        occupant.ProofPhotoUrl = dto.ProofPhotoUrl ?? occupant.ProofPhotoUrl;

        _bookingOccupantRepository.Update(occupant);
        await _bookingOccupantRepository.SaveChangesAsync();

        return MapBookingOccupant(occupant);
    }

    public async Task RemoveOccupantAsync(Guid bookingId, Guid tenantUserId, int occupantOrder)
    {
        if (_bookingOccupantRepository == null)
        {
            throw new InvalidOperationException("Booking occupant persistence is not configured.");
        }

        _ = await EnsureBookingOwnedByTenantOrLandlordAsync(bookingId, tenantUserId);

        var existing = (await _bookingOccupantRepository.FindAsync(o => o.BookingId == bookingId)).ToList();
        var occupant = existing.FirstOrDefault(o => o.OccupantOrder == occupantOrder)
            ?? throw new ArgumentException("Occupant not found for this booking.");

        if (existing.Count <= 1)
        {
            throw new InvalidOperationException("At least one occupant must remain on the booking.");
        }

        var wasPrimary = occupant.IsPrimary;
        _bookingOccupantRepository.Remove(occupant);

        if (wasPrimary)
        {
            var promote = existing.Where(o => o.OccupantId != occupant.OccupantId)
                .OrderBy(o => o.OccupantOrder)
                .FirstOrDefault();
            if (promote != null)
            {
                promote.IsPrimary = true;
                _bookingOccupantRepository.Update(promote);
            }
        }

        await _bookingOccupantRepository.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ResidenceReportOccupantDto>> FillOccupantsManuallyAsync(Guid bookingId, Guid tenantUserId, FillBookingOccupantsDto dto)
    {
        if (_bookingOccupantRepository == null)
        {
            throw new InvalidOperationException("Booking occupant persistence is not configured.");
        }

        if (dto.Occupants == null || dto.Occupants.Count == 0)
        {
            throw new InvalidOperationException("At least one occupant is required.");
        }

        var booking = await EnsureBookingOwnedByLandlordAsync(bookingId, tenantUserId);
        var occupantCap = ResolveBookingOccupantCap(booking);

        if (dto.Occupants.Count > occupantCap)
        {
            throw new InvalidOperationException($"This booking allows at most {occupantCap} occupant(s) based on the requested headcount.");
        }

        var duplicateOrder = dto.Occupants
            .GroupBy(o => o.OccupantOrder)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateOrder != null)
        {
            throw new InvalidOperationException("Occupant order must be unique for this booking.");
        }

        var normalized = dto.Occupants
            .OrderBy(o => o.OccupantOrder)
            .Select(o => new FillBookingOccupantItemDto
            {
                OccupantOrder = o.OccupantOrder,
                IsPrimary = o.IsPrimary,
                FullName = o.FullName,
                PassportId = o.PassportId,
                DateOfBirth = o.DateOfBirth,
                NationalIdCardNumber = o.NationalIdCardNumber,
                Nationality = o.Nationality,
                Sex = o.Sex,
                Phone = o.Phone,
                Email = o.Email,
                ProofPhotoUrl = null
            })
            .ToList();

        if (!normalized.Any(o => o.IsPrimary))
        {
            normalized[0].IsPrimary = true;
        }

        EnsureNoDuplicateOccupantsInPayload(normalized);

        var existing = (await _bookingOccupantRepository.FindAsync(o => o.BookingId == bookingId)).ToList();
        foreach (var item in existing)
        {
            _bookingOccupantRepository.Remove(item);
        }

        var now = Common.Utils.VietnamTime.Now;
        var entities = new List<BookingOccupant>();
        var primaryAssigned = false;

        foreach (var occupant in normalized)
        {
            var entity = new BookingOccupant
            {
                OccupantId = Guid.NewGuid(),
                BookingId = bookingId,
                OccupantOrder = occupant.OccupantOrder,
                IsPrimary = occupant.IsPrimary && !primaryAssigned,
                FullName = occupant.FullName,
                PassportId = occupant.PassportId,
                DateOfBirth = occupant.DateOfBirth,
                NationalIdCardNumber = occupant.NationalIdCardNumber,
                Nationality = occupant.Nationality,
                Sex = occupant.Sex,
                Phone = occupant.Phone,
                Email = occupant.Email,
                ProofPhotoUrl = null,
                CreatedAt = now
            };

            if (entity.IsPrimary)
            {
                primaryAssigned = true;
            }

            entities.Add(entity);
            await _bookingOccupantRepository.AddAsync(entity);
        }

        if (!primaryAssigned && entities.Count > 0)
        {
            entities[0].IsPrimary = true;
            _bookingOccupantRepository.Update(entities[0]);
        }

        await _bookingOccupantRepository.SaveChangesAsync();

        return entities
            .OrderBy(o => o.OccupantOrder)
            .Select(MapBookingOccupant)
            .ToList();
    }

    private async Task<Booking> EnsureBookingOwnedByTenantAsync(Guid bookingId, Guid tenantUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        if (booking.TenantId != tenantUserId)
        {
            throw new InvalidOperationException("You are not allowed to manage occupants for this booking.");
        }

        return booking;
    }

    private async Task<Booking> EnsureBookingOwnedByTenantOrLandlordAsync(Guid bookingId, Guid requesterUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        if (booking.TenantId == requesterUserId)
        {
            return booking;
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        if (apartment.LandlordId == requesterUserId)
        {
            return booking;
        }

        throw new InvalidOperationException("You are not allowed to manage occupants for this booking.");
    }

    private async Task<Booking> EnsureBookingOwnedByLandlordAsync(Guid bookingId, Guid landlordUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        if (apartment.LandlordId != landlordUserId)
        {
            throw new InvalidOperationException("You are not allowed to manage occupants for this booking.");
        }

        return booking;
    }
    private async Task<List<ResidenceReportOccupantDto>> BuildReportOccupantsAsync(Booking booking, TemporaryResidenceReport? report, User? tenantUser)
    {
        if (_bookingOccupantRepository != null)
        {
            var persisted = (await _bookingOccupantRepository.FindAsync(o => o.BookingId == booking.BookingId))
                .OrderBy(o => o.OccupantOrder)
                .ToList();

            if (persisted.Count > 0)
            {
                return persisted.Select(MapBookingOccupant).ToList();
            }
        }

        var primary = new ResidenceReportOccupantDto
        {
            Order = 1,
            IsPrimary = true,
            FullName = tenantUser?.FullName,
            PassportId = report?.TenantPassportId,
            DateOfBirth = tenantUser?.Birthday,
            NationalIdCardNumber = tenantUser?.NationalIdCardNumber,
            Nationality = report?.TenantNationality,
            Sex = tenantUser?.Sex,
            Phone = tenantUser?.Phone,
            Email = tenantUser?.Email,
            ProofPhotoUrl = null
        };

        var fallbackCount = Math.Max(1, (booking.NoOfAdults ?? 0) + (booking.NoOfChildren ?? 0) + (booking.NoOfInfants ?? 0));
        return Enumerable.Range(1, fallbackCount)
            .Select(index => new ResidenceReportOccupantDto
            {
                Order = index,
                IsPrimary = index == 1,
                FullName = primary.FullName,
                PassportId = primary.PassportId,
                DateOfBirth = primary.DateOfBirth,
                NationalIdCardNumber = primary.NationalIdCardNumber,
                Nationality = primary.Nationality,
                Sex = primary.Sex,
                Phone = primary.Phone,
                Email = primary.Email,
                ProofPhotoUrl = primary.ProofPhotoUrl
            })
            .ToList();
    }

    private static ResidenceReportOccupantDto MapBookingOccupant(BookingOccupant occupant)
    {
        return new ResidenceReportOccupantDto
        {
            Order = occupant.OccupantOrder,
            IsPrimary = occupant.IsPrimary,
            FullName = occupant.FullName,
            PassportId = occupant.PassportId,
            DateOfBirth = occupant.DateOfBirth,
            NationalIdCardNumber = occupant.NationalIdCardNumber,
            Nationality = occupant.Nationality,
            Sex = occupant.Sex,
            Phone = occupant.Phone,
            Email = occupant.Email,
            ProofPhotoUrl = occupant.ProofPhotoUrl
        };
    }

    private static int ResolveBookingOccupantCap(Booking booking)
    {
        return Math.Max(1, (booking.NoOfAdults ?? 0) + (booking.NoOfChildren ?? 0) + (booking.NoOfInfants ?? 0));
    }

    private static void EnsureNoDuplicateOccupantIdentity(
        IEnumerable<BookingOccupant> existing,
        string? passportId,
        string? nationalIdCardNumber,
        string? fullName,
        DateOnly? dateOfBirth,
        Guid? excludedOccupantId = null)
    {
        var normalizedPassportId = NormalizeIdentityValue(passportId);
        var normalizedNationalIdCardNumber = NormalizeIdentityValue(nationalIdCardNumber);
        var normalizedFullName = NormalizeIdentityValue(fullName);

        var hasNameAndBirthDate = normalizedFullName != null && dateOfBirth.HasValue;

        var duplicate = existing.FirstOrDefault(o =>
            (!excludedOccupantId.HasValue || o.OccupantId != excludedOccupantId.Value)
            &&
            (
                (normalizedPassportId != null
                    && NormalizeIdentityValue(o.PassportId) == normalizedPassportId)
                ||
                (normalizedNationalIdCardNumber != null
                    && NormalizeIdentityValue(o.NationalIdCardNumber) == normalizedNationalIdCardNumber)
                ||
                (hasNameAndBirthDate
                    && NormalizeIdentityValue(o.FullName) == normalizedFullName
                    && o.DateOfBirth.HasValue
                    && o.DateOfBirth.Value == dateOfBirth!.Value)
            ));

        if (duplicate != null)
        {
            throw new InvalidOperationException("An occupant with the same identity already exists for this booking.");
        }
    }

    private static void EnsureNoDuplicateOccupantsInPayload(IReadOnlyCollection<FillBookingOccupantItemDto> occupants)
    {
        var passportIds = new HashSet<string>(StringComparer.Ordinal);
        var nationalIdCardNumbers = new HashSet<string>(StringComparer.Ordinal);
        var fullNameAndBirthDates = new HashSet<string>(StringComparer.Ordinal);

        foreach (var occupant in occupants)
        {
            var normalizedPassportId = NormalizeIdentityValue(occupant.PassportId);
            if (normalizedPassportId != null && !passportIds.Add(normalizedPassportId))
            {
                throw new InvalidOperationException("Duplicate passport ID found in submitted occupants.");
            }

            var normalizedNationalIdCardNumber = NormalizeIdentityValue(occupant.NationalIdCardNumber);
            if (normalizedNationalIdCardNumber != null && !nationalIdCardNumbers.Add(normalizedNationalIdCardNumber))
            {
                throw new InvalidOperationException("Duplicate national ID card number found in submitted occupants.");
            }

            var normalizedFullName = NormalizeIdentityValue(occupant.FullName);
            if (normalizedFullName != null && occupant.DateOfBirth.HasValue)
            {
                var key = $"{normalizedFullName}|{occupant.DateOfBirth.Value:yyyy-MM-dd}";
                if (!fullNameAndBirthDates.Add(key))
                {
                    throw new InvalidOperationException("Duplicate full name and date of birth found in submitted occupants.");
                }
            }
        }
    }

    private static string? NormalizeIdentityValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    private async Task EnsureNoConflictingBookingsAsync(Guid apartmentId, DateOnly checkInDate, DateOnly checkOutDate)
    {
        await ExpireUnpaidBookingsIfOverdueAsync(apartmentId);

        var blockingStatuses = new[] { "negotiating", "confirmed", "paid", "completed", "disputed" };

        var conflicts = await _bookingRepository.FindAsync(b =>
            b.ApartmentId == apartmentId &&
            b.Status != null &&
            blockingStatuses.Contains(b.Status) &&
            b.CheckInDate < checkOutDate &&
            b.CheckOutDate > checkInDate);

        if (conflicts.Any())
        {
            throw new InvalidOperationException("Apartment is not available for the selected dates.");
        }

        var blackoutConflicts = await _apartmentAvailabilityRepository.FindAsync(a =>
            a.ApartmentId == apartmentId &&
            a.StartDate < checkOutDate &&
            a.EndDate > checkInDate);

        if (blackoutConflicts.Any())
        {
            throw new InvalidOperationException("Apartment is not available for the selected dates.");
        }
    }

    public async Task<BookingCheckTimeResponseDto> RecordCheckInAsync(Guid bookingId, RecordCheckInDto dto, Guid recordedBy)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
            throw new KeyNotFoundException("Apartment not found.");

        await EnsureActorIsOwnerOrStaffAsync(recordedBy, apartment.LandlordId);

        // Validate booking status (must be confirmed or paid)
        if (!checkTime.ActualCheckIn.HasValue &&
            !(string.Equals(booking.Status, "confirmed", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Booking must be in confirmed or paid status to record check-in.");
        }

        // Validate time range: ±1 day from scheduled check-in date
        var scheduledDate = checkTime.ScheduledCheckIn.Date;
        var actualDate = dto.ActualCheckIn.Date;
        var dayDiff = Math.Abs((scheduledDate - actualDate).Days);
        if (dayDiff > 1)
        {
            throw new InvalidOperationException($"Check-in time must be within ±1 day of scheduled check-in date ({scheduledDate:yyyy-MM-dd}).");
        }

        // Check correction window: if already recorded, only allow edits within 24 hours
        if (checkTime.ActualCheckIn.HasValue && checkTime.RecordedAt.HasValue)
        {
            var correctionWindowHours = _configuration.GetValue<int>("BookingCheckTimeSettings:CorrectionWindowHours", 24);
            var timeSinceRecording = Common.Utils.VietnamTime.Now - checkTime.RecordedAt.Value;
            if (timeSinceRecording.TotalHours > correctionWindowHours)
            {
                throw new InvalidOperationException($"Check-in time cannot be modified after {correctionWindowHours} hours of initial recording. Contact support to dispute.");
            }
        }

        // Detect early check-in and calculate fee
        bool isEarlyCheckIn = dto.ActualCheckIn < checkTime.ScheduledCheckIn;
        decimal earlyCheckInFee = 0m;

        if (isEarlyCheckIn)
        {
            var earlyFeePercent = _configuration.GetValue<decimal>("BookingCheckTimeSettings:EarlyCheckInFeePercentOfDaily", 0.5m);
            earlyCheckInFee = Math.Round(booking.TotalPrice / booking.Nights * earlyFeePercent, 2, MidpointRounding.AwayFromZero);
        }

        // Update check-in record
        checkTime.ActualCheckIn = dto.ActualCheckIn;
        checkTime.IsEarlyCheckIn = isEarlyCheckIn;
        checkTime.EarlyCheckInFee = isEarlyCheckIn ? earlyCheckInFee : 0m;
        checkTime.CheckInPhotoUrl = dto.PhotoEvidenceUrl;
        ApplyFeeSettlementState(checkTime, earlyCheckInFee, Common.Utils.VietnamTime.Now);
        checkTime.RecordedBy = recordedBy;
        checkTime.RecordedAt = Common.Utils.VietnamTime.Now;
        checkTime.TenantResponseStatus = "pending";
        checkTime.TenantRespondedBy = null;
        checkTime.TenantRespondedAt = null;
        checkTime.TenantDisputeReason = null;
        checkTime.TenantDisputeNotes = null;
        checkTime.DisputeResolutionStatus = null;
        checkTime.DisputeResolvedBy = null;
        checkTime.DisputeResolvedAt = null;
        checkTime.DisputeResolutionNotes = null;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            checkTime.Notes = dto.Notes;
        }

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        // Emit audit event for check-in
        await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, recordedBy, "check_in_recorded", new { ActualCheckIn = dto.ActualCheckIn, Notes = dto.Notes, Photo = dto.PhotoEvidenceUrl });

        // Notify landlord
        var checkInMessage = isEarlyCheckIn
            ? $"Guest arrived early at {dto.ActualCheckIn:yyyy-MM-dd HH:mm}. Early check-in fee: ${earlyCheckInFee}"
            : $"Guest checked in at {dto.ActualCheckIn:yyyy-MM-dd HH:mm} (on schedule).";

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.check_in_recorded.ToString(),
            "Check-in Recorded",
            checkInMessage,
            bookingId);

        return await GetCheckTimeDetailsAsync(bookingId, apartment.LandlordId);
    }

    public async Task<BookingCheckTimeResponseDto> RecordCheckOutAsync(Guid bookingId, RecordCheckOutDto dto, Guid recordedBy)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
            throw new KeyNotFoundException("Apartment not found.");

        await EnsureActorIsOwnerOrStaffAsync(recordedBy, apartment.LandlordId);

        // Validate that check-in has been recorded
        if (!checkTime.ActualCheckIn.HasValue)
        {
            throw new InvalidOperationException("Cannot record check-out before check-in has been recorded.");
        }

        // Validate time range: ±1 day from scheduled check-out date
        var scheduledDate = checkTime.ScheduledCheckOut.Date;
        var actualDate = dto.ActualCheckOut.Date;
        var dayDiff = Math.Abs((scheduledDate - actualDate).Days);
        if (dayDiff > 1)
        {
            throw new InvalidOperationException($"Check-out time must be within ±1 day of scheduled check-out date ({scheduledDate:yyyy-MM-dd}).");
        }

        // Check correction window: if already recorded, only allow edits within 24 hours
        if (checkTime.ActualCheckOut.HasValue)
        {
            var recordedCheckOutTime = checkTime.UpdatedAt ?? checkTime.RecordedAt;
            if (recordedCheckOutTime.HasValue)
            {
                var correctionWindowHours = _configuration.GetValue<int>("BookingCheckTimeSettings:CorrectionWindowHours", 24);
                var timeSinceRecording = Common.Utils.VietnamTime.Now - recordedCheckOutTime.Value;
                if (timeSinceRecording.TotalHours > correctionWindowHours)
                {
                    throw new InvalidOperationException($"Check-out time cannot be modified after {correctionWindowHours} hours of initial recording. Contact support to dispute.");
                }
            }
        }

        // Detect late check-out and calculate fee
        bool isLateCheckOut = dto.ActualCheckOut > checkTime.ScheduledCheckOut;
        decimal lateCheckOutFee = 0m;

        if (isLateCheckOut)
        {
            var lateFePercentPerHour = _configuration.GetValue<decimal>("BookingCheckTimeSettings:LateCheckOutFeePercentPerHour", 0.025m);
            var hoursLate = (decimal)Math.Ceiling((dto.ActualCheckOut - checkTime.ScheduledCheckOut).TotalHours);
            lateCheckOutFee = Math.Round(booking.TotalPrice / booking.Nights * lateFePercentPerHour * hoursLate, 2, MidpointRounding.AwayFromZero);
        }

        // Update check-out record
        var now = Common.Utils.VietnamTime.Now;
        checkTime.ActualCheckOut = dto.ActualCheckOut;
        checkTime.IsLateCheckOut = isLateCheckOut;
        checkTime.LateCheckOutFee = isLateCheckOut ? lateCheckOutFee : 0m;
        checkTime.CheckOutPhotoUrl = dto.PhotoEvidenceUrl;
        checkTime.ClaimOpenedAt = now;
        checkTime.ClaimExpiresAt = now.AddHours(24);
        checkTime.ClaimLockedAt = null;
        checkTime.ClaimStatus = "open";
        checkTime.NoShowStatus = null;
        checkTime.NoShowMarkedAt = null;
        checkTime.NoShowMarkedBy = null;
        checkTime.MissingCheckOutStatus = null;
        checkTime.AutoClosedAt = null;
        ApplyFeeSettlementState(checkTime, GetCheckTimeFeeTotal(checkTime), now);
        if (GetCheckTimeFeeTotal(checkTime) > 0m)
        {
            // Claim window controls when the fee becomes due for tenant settlement.
            checkTime.FeeDueAt = checkTime.ClaimExpiresAt;
        }
        checkTime.UpdatedAt = now;
        checkTime.TenantResponseStatus = "pending";
        checkTime.TenantRespondedBy = null;
        checkTime.TenantRespondedAt = null;
        checkTime.TenantDisputeReason = null;
        checkTime.TenantDisputeNotes = null;
        checkTime.DisputeResolutionStatus = null;
        checkTime.DisputeResolvedBy = null;
        checkTime.DisputeResolvedAt = null;
        checkTime.DisputeResolutionNotes = null;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            checkTime.Notes = dto.Notes;
        }

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        // Update booking status to "completed"
        booking.Status = "completed";
        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        // (audit event for claim resolution is emitted in dispute resolution flow)

        // Credit landlord wallet after successful checkout
        var paymentMode = GetBookingPaymentMode(booking);
        var totalCreditAmount = paymentMode == BookingPaymentMode.full
            ? Math.Round(booking.TotalPrice * FullPaymentLandlordShareRate, 2, MidpointRounding.AwayFromZero)
            : Math.Round(GetUpfrontPaymentAmount(booking) * FullPaymentLandlordShareRate, 2, MidpointRounding.AwayFromZero);

        // Add remaining balance if not full payment
        if (paymentMode != BookingPaymentMode.full)
        {
            var remainingAmount = booking.TotalPrice - GetUpfrontPaymentAmount(booking);
            if (remainingAmount > 0)
            {
                var remainingLandlordShare = Math.Round(remainingAmount * FullPaymentLandlordShareRate, 2, MidpointRounding.AwayFromZero);
                totalCreditAmount += remainingLandlordShare;
            }
        }

        await _landlordWalletService.CreditPendingAsync(apartment.LandlordId, totalCreditAmount);

        // Notify landlord
        var checkOutMessage = isLateCheckOut
            ? $"Guest checked out late at {dto.ActualCheckOut:yyyy-MM-dd HH:mm}. Late check-out fee: ${lateCheckOutFee}"
            : $"Guest checked out at {dto.ActualCheckOut:yyyy-MM-dd HH:mm} (on schedule).";

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.check_out_recorded.ToString(),
            "Check-out Recorded",
            $"{checkOutMessage} Payment of {totalCreditAmount:0.00} has been credited to your wallet.",
            bookingId);

        // Notify tenant
        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.check_out_recorded.ToString(),
            "Claim Opened",
            $"A claim snapshot has been opened for your checkout and will lock at {checkTime.ClaimExpiresAt:yyyy-MM-dd HH:mm}. {(isLateCheckOut ? $"Late checkout fee: ${lateCheckOutFee}" : "Thank you for checking out on time!")}",
            bookingId);

        return await GetCheckTimeDetailsAsync(bookingId, apartment.LandlordId);
    }

    public async Task<BookingCheckTimeResponseDto> GetCheckTimeDetailsAsync(Guid bookingId, Guid? requesterId = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);

        // Calculate if still editable (within 24-hour window from RecordedAt)
        bool isEditable = false;
        if (checkTime.RecordedAt.HasValue)
        {
            var correctionWindowHours = _configuration.GetValue<int>("BookingCheckTimeSettings:CorrectionWindowHours", 24);
            var timeSinceRecording = Common.Utils.VietnamTime.Now - checkTime.RecordedAt.Value;
            isEditable = timeSinceRecording.TotalHours <= correctionWindowHours;
        }

        var now = Common.Utils.VietnamTime.Now;
        var claimExpiresAt = checkTime.ClaimExpiresAt;
        var claimIsExpired = claimExpiresAt.HasValue && now >= claimExpiresAt.Value;
        var claimLockedAt = checkTime.ClaimLockedAt ?? (claimIsExpired ? claimExpiresAt : null);
        var claimStatus = string.IsNullOrWhiteSpace(checkTime.ClaimStatus)
            ? (claimLockedAt.HasValue ? "locked" : "open")
            : checkTime.ClaimStatus.Trim().ToLowerInvariant();

        return new BookingCheckTimeResponseDto
        {
            CheckTimeId = checkTime.CheckTimeId,
            BookingId = checkTime.BookingId,
            ScheduledCheckIn = checkTime.ScheduledCheckIn,
            ScheduledCheckOut = checkTime.ScheduledCheckOut,
            ActualCheckIn = checkTime.ActualCheckIn,
            ActualCheckOut = checkTime.ActualCheckOut,
            IsEarlyCheckIn = checkTime.IsEarlyCheckIn,
            EarlyCheckInFee = checkTime.EarlyCheckInFee,
            IsLateCheckOut = checkTime.IsLateCheckOut,
            LateCheckOutFee = checkTime.LateCheckOutFee,
            TotalFee = (checkTime.EarlyCheckInFee ?? 0m) + (checkTime.LateCheckOutFee ?? 0m),
            FeeSettlementStatus = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime),
            FeeDueAt = checkTime.FeeDueAt,
            FeeSettledAt = checkTime.FeeSettledAt,
            FeeSettlementNotes = checkTime.FeeSettlementNotes,
            ManualSettlementRequired = IsFeeSettlementRequired(checkTime, Common.Utils.VietnamTime.Now),
            RecordedBy = checkTime.RecordedBy,
            RecordedAt = checkTime.RecordedAt,
            Notes = checkTime.Notes,
            CheckInPhotoUrl = checkTime.CheckInPhotoUrl,
            CheckOutPhotoUrl = checkTime.CheckOutPhotoUrl,
            ClaimOpenedAt = checkTime.ClaimOpenedAt,
            ClaimExpiresAt = claimExpiresAt,
            ClaimLockedAt = claimLockedAt,
            ClaimStatus = claimStatus,
            ClaimIsLocked = claimLockedAt.HasValue,
            ClaimIsExpired = claimIsExpired,
            NoShowStatus = checkTime.NoShowStatus,
            NoShowMarkedBy = checkTime.NoShowMarkedBy,
            NoShowMarkedAt = checkTime.NoShowMarkedAt,
            MissingCheckOutStatus = checkTime.MissingCheckOutStatus,
            AutoClosedAt = checkTime.AutoClosedAt,
            LastModifiedAt = checkTime.UpdatedAt ?? checkTime.RecordedAt,
            IsEditable = isEditable,
            TenantResponseStatus = string.IsNullOrWhiteSpace(checkTime.TenantResponseStatus) ? "pending" : checkTime.TenantResponseStatus,
            TenantRespondedBy = checkTime.TenantRespondedBy,
            TenantRespondedAt = checkTime.TenantRespondedAt,
            TenantDisputeReason = checkTime.TenantDisputeReason,
            TenantDisputeNotes = checkTime.TenantDisputeNotes,
            DisputeResolutionStatus = checkTime.DisputeResolutionStatus,
            DisputeResolvedBy = checkTime.DisputeResolvedBy,
            DisputeResolvedAt = checkTime.DisputeResolvedAt,
            DisputeResolutionNotes = checkTime.DisputeResolutionNotes
        };
    }

    public async Task<BookingCheckTimeResponseDto> RespondToCheckTimeAsync(Guid bookingId, Guid tenantId, RespondBookingCheckTimeDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.TenantId != tenantId)
        {
            throw new InvalidOperationException("You are not allowed to respond to this booking check-time.");
        }

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);

        if (!checkTime.ActualCheckIn.HasValue && !checkTime.ActualCheckOut.HasValue)
        {
            throw new InvalidOperationException("No recorded check-in/check-out found to respond to.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new KeyNotFoundException("Apartment not found.");

        var claimExpiresAt = checkTime.ClaimExpiresAt ?? checkTime.ClaimOpenedAt?.AddHours(24) ?? checkTime.RecordedAt?.AddHours(24);
        if (claimExpiresAt.HasValue && Common.Utils.VietnamTime.Now >= claimExpiresAt.Value)
        {
            checkTime.ClaimLockedAt ??= claimExpiresAt;
            checkTime.ClaimStatus = "locked";
            _bookingCheckTimeRepository.Update(checkTime);
            await _bookingCheckTimeRepository.SaveChangesAsync();
            throw new InvalidOperationException("This claim is locked and can no longer be refuted.");
        }

        var action = dto.Action.Trim().ToLowerInvariant();
        if (action != "confirm" && action != "refute" && action != "dispute")
        {
            throw new InvalidOperationException("Action must be 'confirm' or 'refute'.");
        }

        var currentStatus = checkTime.TenantResponseStatus?.Trim().ToLowerInvariant();
        if (string.Equals(currentStatus, action, StringComparison.OrdinalIgnoreCase))
        {
            return await GetCheckTimeDetailsAsync(bookingId, tenantId);
        }

        checkTime.TenantRespondedBy = tenantId;
        checkTime.TenantRespondedAt = Common.Utils.VietnamTime.Now;

        if (action == "confirm")
        {
            checkTime.TenantResponseStatus = "confirmed";
            checkTime.ClaimStatus = "confirmed";
            checkTime.TenantDisputeReason = null;
            checkTime.TenantDisputeNotes = null;
            checkTime.DisputeResolutionStatus = null;
            checkTime.DisputeResolvedBy = null;
            checkTime.DisputeResolvedAt = null;
            checkTime.DisputeResolutionNotes = null;

            _bookingCheckTimeRepository.Update(checkTime);
            await _bookingCheckTimeRepository.SaveChangesAsync();

            await CreateBookingNotificationAsync(
                apartment.LandlordId,
                NotificationType.check_time_confirmed.ToString(),
                "Check-time Confirmed",
                "Tenant has confirmed the recorded check-in/check-out details.",
                booking.BookingId);

            await CreateBookingNotificationAsync(
                tenantId,
                NotificationType.check_time_confirmed.ToString(),
                "Check-time Confirmed",
                "Your confirmation has been recorded successfully.",
                booking.BookingId);

            return await GetCheckTimeDetailsAsync(bookingId, tenantId);
        }

        if (string.IsNullOrWhiteSpace(dto.DisputeReason))
        {
            throw new InvalidOperationException("Refute reason is required.");
        }

        checkTime.TenantResponseStatus = "refuted";
        checkTime.ClaimStatus = "refuted";
        checkTime.TenantDisputeReason = dto.DisputeReason.Trim();
        checkTime.TenantDisputeNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        checkTime.DisputeResolutionStatus = "open";
        checkTime.DisputeResolvedBy = null;
        checkTime.DisputeResolvedAt = null;
        checkTime.DisputeResolutionNotes = null;
        checkTime.FeeSettlementStatus = FeeSettlementStatusDisputed;

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        booking.Status = BookingStatus.disputed.ToString();
        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.check_time_disputed.ToString(),
            "Claim Refuted",
            $"Tenant refuted the claim. Reason: {checkTime.TenantDisputeReason}",
            booking.BookingId);

        await CreateBookingNotificationAsync(
            tenantId,
            NotificationType.check_time_disputed.ToString(),
            "Claim Submitted",
            "Your refutation has been submitted and is waiting for staff/admin resolution.",
            booking.BookingId);

        return await GetCheckTimeDetailsAsync(bookingId, tenantId);
    }

    public async Task<BookingCheckTimeResponseDto> ResolveCheckTimeDisputeAsync(Guid bookingId, Guid resolvedBy, ResolveBookingCheckTimeDisputeDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        await EnsureIsStaffOrAdminAsync(resolvedBy);

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);

        var isDisputed = string.Equals(checkTime.TenantResponseStatus, "disputed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checkTime.TenantResponseStatus, "refuted", StringComparison.OrdinalIgnoreCase);
        var isOpen = string.Equals(checkTime.DisputeResolutionStatus, "open", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(checkTime.DisputeResolutionStatus);

        if (!isDisputed || !isOpen)
        {
            throw new InvalidOperationException("No open tenant claim found for this booking.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new KeyNotFoundException("Apartment not found.");

        checkTime.DisputeResolvedBy = resolvedBy;
        checkTime.DisputeResolvedAt = Common.Utils.VietnamTime.Now;
        checkTime.DisputeResolutionNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        checkTime.DisputeResolutionStatus = dto.ApproveTenantDispute
            ? "resolved_in_favor_of_tenant"
            : "resolved_in_favor_of_landlord";
        if (dto.ApproveTenantDispute)
        {
            checkTime.FeeSettlementStatus = FeeSettlementStatusWaived;
            checkTime.FeeSettledAt = Common.Utils.VietnamTime.Now;
            checkTime.FeeDueAt = null;
        }
        else if (GetCheckTimeFeeTotal(checkTime) > 0m)
        {
            checkTime.FeeSettlementStatus = FeeSettlementStatusDue;
            checkTime.FeeDueAt ??= Common.Utils.VietnamTime.Now.AddDays(GetFeeSettlementGraceDays());
            checkTime.FeeSettledAt = null;
        }

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        if (string.Equals(booking.Status, BookingStatus.disputed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (checkTime.ActualCheckOut.HasValue)
            {
                booking.Status = BookingStatus.completed.ToString();
            }
            else if (checkTime.ActualCheckIn.HasValue)
            {
                booking.Status = BookingStatus.paid.ToString();
            }
            else
            {
                booking.Status = BookingStatus.confirmed.ToString();
            }

            _bookingRepository.Update(booking);
            await _bookingRepository.SaveChangesAsync();
            await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);
        }

        var resolutionMessage = dto.ApproveTenantDispute
            ? "A claim was resolved in favor of tenant."
            : "A claim was resolved in favor of landlord.";

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.check_time_dispute_resolved.ToString(),
            "Check-time Dispute Resolved",
            resolutionMessage,
            booking.BookingId);

        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.check_time_dispute_resolved.ToString(),
            "Check-time Dispute Resolved",
            resolutionMessage,
            booking.BookingId);

        await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, resolvedBy, "claim_resolved", new { ResolvedInFavorOfTenant = dto.ApproveTenantDispute, Notes = dto.Notes });

        return await GetCheckTimeDetailsAsync(bookingId, resolvedBy);
    }

    public async Task<BookingCheckTimeResponseDto> SettleCheckTimeFeeAsync(Guid bookingId, Guid settledBy, SettleBookingCheckTimeFeeDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        var totalFee = GetCheckTimeFeeTotal(checkTime);
        if (totalFee <= 0m)
        {
            throw new InvalidOperationException("No check-time fee is available for settlement.");
        }

        var settlementAction = dto.SettlementAction.Trim().ToLowerInvariant();
        if (settlementAction != FeeSettlementStatusPaid && settlementAction != FeeSettlementStatusWaived)
        {
            throw new InvalidOperationException("SettlementAction must be 'paid' or 'waived'.");
        }

        checkTime.FeeSettlementStatus = settlementAction;
        checkTime.FeeSettledAt = Common.Utils.VietnamTime.Now;
        checkTime.FeeDueAt = null;
        checkTime.FeeSettlementNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        checkTime.TenantResponseStatus = settlementAction == FeeSettlementStatusPaid ? "confirmed" : checkTime.TenantResponseStatus;

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            var title = settlementAction == FeeSettlementStatusPaid ? "Check-time Fee Settled" : "Check-time Fee Waived";
            var message = settlementAction == FeeSettlementStatusPaid
                ? $"A check-time fee of {totalFee:0.00} was marked as paid for booking {booking.BookingId}."
                : $"A check-time fee of {totalFee:0.00} was waived for booking {booking.BookingId}.";

            await CreateBookingNotificationAsync(
                booking.TenantId,
                NotificationType.system_announcement.ToString(),
                title,
                message,
                booking.BookingId);

            await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, settledBy, "fee_settled", new { SettlementAction = settlementAction, Notes = dto.Notes });

            await CreateBookingNotificationAsync(
                apartment.LandlordId,
                NotificationType.system_announcement.ToString(),
                title,
                message,
                booking.BookingId);
        }

        return await GetCheckTimeDetailsAsync(bookingId, settledBy);
    }

    public async Task<BookingCheckTimeResponseDto> SubmitPaymentConfirmationAsync(Guid bookingId, Guid landlordId, LandlordPaymentConfirmationDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new KeyNotFoundException("Apartment not found.");

        if (apartment.LandlordId != landlordId)
        {
            throw new InvalidOperationException("Only the apartment landlord can submit payment confirmations.");
        }

        var totalFee = GetCheckTimeFeeTotal(checkTime);
        if (totalFee <= 0m)
        {
            throw new InvalidOperationException("No check-time fee is available for this booking.");
        }

        var currentStatus = string.IsNullOrWhiteSpace(checkTime.FeeSettlementStatus) ? "none" : checkTime.FeeSettlementStatus.ToLowerInvariant();
        if (currentStatus != FeeSettlementStatusDue)
        {
            throw new InvalidOperationException($"Fee settlement status must be 'due' to submit payment confirmation. Current status: {currentStatus}");
        }

        var now = Common.Utils.VietnamTime.Now;
        // Store payment evidence in FeeSettlementNotes
        var paymentEvidence = $"[LANDLORD SUBMITTED PAYMENT] Amount: Date: {dto.PaymentDate:yyyy-MM-dd HH:mm:ss}";

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            paymentEvidence += $", Notes: {dto.Notes}";
        }

        checkTime.FeeSettlementStatus = FeeSettlementStatusPaid;
        checkTime.FeeSettledAt = now;
        checkTime.FeeDueAt = null;
        checkTime.FeeSettlementNotes = paymentEvidence;
        checkTime.TenantResponseStatus = "confirmed";
        checkTime.UpdatedAt = now;

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        // Notify landlord of submission
        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.system_announcement.ToString(),
            "Payment Confirmation Submitted",
            $"Your payment confirmation for booking {booking.BookingId} has been submitted.",
            booking.BookingId);

        return await GetCheckTimeDetailsAsync(bookingId, landlordId);
    }

    public async Task<BookingCheckTimeResponseDto> PayClaimFeeAsync(Guid bookingId, Guid tenantId, PayClaimFeeDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.TenantId != tenantId)
        {
            throw new InvalidOperationException("You are not allowed to pay claim fees for this booking.");
        }

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        var totalFee = GetCheckTimeFeeTotal(checkTime);
        if (totalFee <= 0m)
        {
            throw new InvalidOperationException("No claim fee is available for payment.");
        }

        var now = Common.Utils.VietnamTime.Now;
        var claimExpiresAt = checkTime.ClaimExpiresAt ?? checkTime.ClaimOpenedAt?.AddHours(24) ?? checkTime.UpdatedAt?.AddHours(24);
        if (!claimExpiresAt.HasValue || now < claimExpiresAt.Value)
        {
            throw new InvalidOperationException("Claim is still open for refutation. Payment becomes required after the 24-hour claim window.");
        }

        if (string.Equals(checkTime.TenantResponseStatus, "refuted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checkTime.TenantResponseStatus, "disputed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Claim is under refutation/dispute and cannot be paid until resolved.");
        }

        var currentStatus = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime);
        if (currentStatus == FeeSettlementStatusPaid)
        {
            return await GetCheckTimeDetailsAsync(bookingId, tenantId);
        }

        if (currentStatus == FeeSettlementStatusWaived)
        {
            throw new InvalidOperationException("Claim fee has already been waived.");
        }

        // Create payment record (pending)
        var method = string.IsNullOrWhiteSpace(dto.PaymentMethod)
    ? "payos"
    : dto.PaymentMethod.Trim().ToLowerInvariant();

        var payment = new DAL.Models.Payment
        {
            PaymentId = Guid.NewGuid(),
            RelatedEntityId = booking.BookingId,
            RelatedEntityType = "booking_check_time",
            Amount = totalFee,
            PaymentType = "charge",
            PaymentPurpose = "check_time_fee",
            LandlordId = booking.ApartmentId == Guid.Empty ? null : (Guid?)null,
            LandlordAmount = 0m,
            PlatformFee = 0m,
            SettlementStatus = "pending",
            Method = method,
            Status = "initiated",
            TransactionId = null,
            PaidAt = null
        };

        await _paymentRepository.AddAsync(payment);
        await _paymentRepository.SaveChangesAsync();


        if (method == "payos")
        {
            var isAndroid = string.Equals(dto.DevicePlatform?.Trim(), "android", StringComparison.OrdinalIgnoreCase);
            var resolvedReturnUrl = string.IsNullOrWhiteSpace(dto.ReturnUrl)
                ? isAndroid
                    ? "VStay://payos-payment"
                    : _stripeSettings.SuccessUrl
                : dto.ReturnUrl;

            var resolvedCancelUrl = string.IsNullOrWhiteSpace(dto.ReturnUrl)
                ? isAndroid
                    ? "VStay://payos-payment"
                    : _stripeSettings.CancelUrl
                : dto.ReturnUrl;

            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payosRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (long)Math.Round(totalFee),
                Description = "Check-time fee",
                ReturnUrl = resolvedReturnUrl,
                CancelUrl = resolvedCancelUrl,
                Items = new List<PaymentLinkItem>
        {
            new PaymentLinkItem
            {
                Name = $"Booking {booking.BookingId}",
                Quantity = 1,
                Price = (long)Math.Round(totalFee),
                Unit = "fee"
            }
        }
            };

            CreatePaymentLinkResponse payosResponse;
            try
            {
                payosResponse = await _payOsClient.PaymentRequests.CreateAsync(payosRequest);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"PayOS payment link creation failed: {ex.Message}");
            }

            payment.TransactionId = payosResponse.PaymentLinkId;
            payment.Status = "pending";
            _paymentRepository.Update(payment);
            await _paymentRepository.SaveChangesAsync();

            checkTime.ClaimLockedAt ??= claimExpiresAt.Value;
            checkTime.ClaimStatus = "payment_pending";
            checkTime.FeeSettlementStatus = "payment_submitted_pending_verification";
            checkTime.FeeSettlementNotes = $"[PAYMENT_INITIATED] Checkout: {payosResponse.CheckoutUrl}";

            _bookingCheckTimeRepository.Update(checkTime);
            await _bookingCheckTimeRepository.SaveChangesAsync();

            var response = await GetCheckTimeDetailsAsync(bookingId, tenantId);
            response.PaymentRedirectUrl = payosResponse.CheckoutUrl;
            response.PendingPaymentId = payment.PaymentId;

            return response;
        }

        // Default to stripe only when explicitly requested.
        if (method == "stripe")
        {
            // Prepare stripe checkout
            var amountMinor = (long)Math.Round(totalFee * 100m, 0, MidpointRounding.AwayFromZero);
            var stripeReq = new Common.DTOs.StripeCheckoutRequestDto
            {
                Amount = amountMinor,
                Currency = _configuration.GetValue<string>("Stripe:Currency", "usd"),
                RelatedEntityId = booking.BookingId,
                PaymentPurpose = "check_time_fee",
                PaymentType = "check_time",
                DevicePlatform = dto.DevicePlatform ?? "web",
                ReturnUrl = dto.ReturnUrl
            };

            var checkout = await _stripeService.CreateCheckoutSessionAsync(stripeReq);

            // Save transaction id
            payment.TransactionId = checkout.SessionId;
            payment.Method = "stripe";
            payment.Status = "pending";
            _paymentRepository.Update(payment);
            await _paymentRepository.SaveChangesAsync();

            // Mark checkTime as awaiting payment verification
            checkTime.ClaimLockedAt ??= claimExpiresAt.Value;
            checkTime.ClaimStatus = "payment_pending";
            checkTime.FeeSettlementStatus = "payment_submitted_pending_verification";
            checkTime.FeeSettlementNotes = $"[PAYMENT_INITIATED] Checkout: {checkout.Url}";

            _bookingCheckTimeRepository.Update(checkTime);
            await _bookingCheckTimeRepository.SaveChangesAsync();

            var response = await GetCheckTimeDetailsAsync(bookingId, tenantId);
            response.PaymentRedirectUrl = checkout.Url;
            response.PendingPaymentId = payment.PaymentId;

            await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, tenantId, "payment_initiated", new { PaymentId = payment.PaymentId, CheckoutUrl = checkout.Url });
            return response;
        }
        // For other methods (e.g., momo), we can implement later; return initiated response
        checkTime.ClaimLockedAt ??= claimExpiresAt.Value;
        checkTime.ClaimStatus = "payment_pending";
        checkTime.FeeSettlementStatus = "payment_submitted_pending_verification";
        checkTime.FeeSettlementNotes = $"[PAYMENT_INITIATED] Method: {payment.Method}";

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

        var fallback = await GetCheckTimeDetailsAsync(bookingId, tenantId);
        fallback.PendingPaymentId = payment.PaymentId;
        await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, tenantId, "payment_initiated", new { PaymentId = payment.PaymentId, Method = payment.Method });
        return fallback;
    }

    public async Task<BookingCheckTimeResponseDto> MarkNoShowAsync(Guid bookingId, Guid actorId, MarkNoShowDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        if (checkTime.ActualCheckIn.HasValue)
        {
            throw new InvalidOperationException("Cannot mark no-show after check-in has already been recorded.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new KeyNotFoundException("Apartment not found.");

        await EnsureActorIsOwnerOrStaffAsync(actorId, apartment.LandlordId);

        var noShowGraceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:NoShowGraceHours", 4);
        var noShowEligibleAt = checkTime.ScheduledCheckIn.AddHours(noShowGraceHours <= 0 ? 4 : noShowGraceHours);
        var now = Common.Utils.VietnamTime.Now;
        if (now < noShowEligibleAt)
        {
            throw new InvalidOperationException($"No-show can only be marked after {noShowEligibleAt:yyyy-MM-dd HH:mm}.");
        }

        checkTime.NoShowStatus = "confirmed";
        checkTime.NoShowMarkedBy = actorId;
        checkTime.NoShowMarkedAt = now;
        checkTime.ClaimStatus = "no_show_confirmed";
        checkTime.ClaimLockedAt = now;
        checkTime.ClaimExpiresAt = now;
        checkTime.TenantResponseStatus = "pending";
        checkTime.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? checkTime.Notes : dto.Notes.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Reason))
        {
            checkTime.FeeSettlementNotes = $"[NO_SHOW] {dto.Reason.Trim()}";
        }

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();
        booking.Status = BookingStatus.cancelled.ToString();

        // Emit audit event for no-show
        await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, actorId, "no_show_marked", new { Reason = dto.Reason, Notes = dto.Notes });

        booking.Status = BookingStatus.cancelled.ToString();
        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.system_announcement.ToString(),
            "No-show Marked",
            "Your booking has been marked as no-show. Contact support if this is incorrect.",
            booking.BookingId);

        return await GetCheckTimeDetailsAsync(bookingId, actorId);
    }

    public async Task<BookingCheckTimeResponseDto> CloseMissingCheckOutAsync(Guid bookingId, Guid actorId, CloseMissingCheckOutDto dto)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        var checkTime = await GetOrCreateBookingCheckTimeAsync(booking);
        if (!checkTime.ActualCheckIn.HasValue)
        {
            throw new InvalidOperationException("Cannot close missing check-out before check-in is recorded.");
        }

        if (checkTime.ActualCheckOut.HasValue)
        {
            return await GetCheckTimeDetailsAsync(bookingId, actorId);
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new KeyNotFoundException("Apartment not found.");

        await EnsureActorIsOwnerOrStaffAsync(actorId, apartment.LandlordId);

        var now = Common.Utils.VietnamTime.Now;
        var graceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:MissingCheckOutGraceHours", 6);
        var eligibleAt = checkTime.ScheduledCheckOut.AddHours(graceHours <= 0 ? 6 : graceHours);
        if (!dto.Force && now < eligibleAt)
        {
            throw new InvalidOperationException($"Missing check-out can only be closed after {eligibleAt:yyyy-MM-dd HH:mm}, or set Force=true.");
        }

        checkTime.ActualCheckOut = checkTime.ScheduledCheckOut;
        checkTime.IsLateCheckOut = false;
        checkTime.LateCheckOutFee = 0m;
        checkTime.MissingCheckOutStatus = dto.Force ? "closed_forced" : "closed_after_grace";
        checkTime.AutoClosedAt = now;
        checkTime.ClaimStatus = "closed";
        checkTime.ClaimOpenedAt ??= now;
        checkTime.ClaimExpiresAt ??= now;
        checkTime.ClaimLockedAt ??= now;
        checkTime.UpdatedAt = now;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            checkTime.Notes = dto.Notes.Trim();
        }

        ApplyFeeSettlementState(checkTime, GetCheckTimeFeeTotal(checkTime), now);

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();
        booking.Status = BookingStatus.completed.ToString();

        // Emit event for missing check-out closed
        await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, actorId, "missing_checkout_closed", new { Force = dto.Force, Notes = dto.Notes });

        booking.Status = BookingStatus.completed.ToString();
        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.system_announcement.ToString(),
            "Missing Check-out Closed",
            "Your booking was closed due to missing check-out record.",
            booking.BookingId);

        return await GetCheckTimeDetailsAsync(bookingId, actorId);
    }

    public async Task ProcessCheckTimeAutomationAsync()
    {
        var now = Common.Utils.VietnamTime.Now;
        // Check for long-running claim queues and emit alert events when thresholds exceeded
        try
        {
            var longClaimDays = _configuration.GetValue<int>("BookingCheckTimeSettings:LongClaimThresholdDays", 7);
            var longClaimAlertCount = _configuration.GetValue<int>("BookingCheckTimeSettings:LongClaimAlertCount", 20);
            if (longClaimDays > 0)
            {
                var longAgo = now.AddDays(-longClaimDays);
                var longClaims = (await _bookingCheckTimeRepository.FindAsync(ct => ct.ClaimOpenedAt.HasValue && ct.ClaimOpenedAt.Value <= longAgo)).ToList();
                if (longClaims.Count >= longClaimAlertCount)
                {
                    var oldest = longClaims.Min(ct => ct!.ClaimOpenedAt!.Value);
                    await CreateCheckTimeStateEventAsync(Guid.Empty, null, null, "long_claim_queue", new { Count = longClaims.Count, ThresholdDays = longClaimDays, Oldest = oldest });
                }
            }
        }
        catch
        {
            // don't fail automation because of alerting checks
        }
        var claimWindowHours = _configuration.GetValue<int>("BookingCheckTimeSettings:ClaimWindowHours", 24);
        if (claimWindowHours <= 0)
        {
            claimWindowHours = 24;
        }

        var noShowGraceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:NoShowGraceHours", 4);
        if (noShowGraceHours <= 0)
        {
            noShowGraceHours = 4;
        }

        var missingCheckOutGraceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:MissingCheckOutGraceHours", 6);
        if (missingCheckOutGraceHours <= 0)
        {
            missingCheckOutGraceHours = 6;
        }

        // Emit automation run started event
        var runId = Guid.NewGuid();
        await CreateCheckTimeStateEventAsync(Guid.Empty, null, null, "automation_run_started", new { RunId = runId, StartedAt = now });

        var processed = 0;
        var checkTimes = await _bookingCheckTimeRepository.FindAsync(_ => true);
        foreach (var checkTime in checkTimes)
        {
            var booking = await _bookingRepository.GetByIdAsync(checkTime.BookingId);
            if (booking == null)
            {
                continue;
            }

            var changed = false;

            var claimExpiresAt = checkTime.ClaimExpiresAt ?? checkTime.ClaimOpenedAt?.AddHours(claimWindowHours);
            var tenantRefuted = string.Equals(checkTime.TenantResponseStatus, "refuted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(checkTime.TenantResponseStatus, "disputed", StringComparison.OrdinalIgnoreCase);
            var feeTotal = GetCheckTimeFeeTotal(checkTime);

            if (claimExpiresAt.HasValue && now >= claimExpiresAt.Value && !checkTime.ClaimLockedAt.HasValue)
            {
                checkTime.ClaimLockedAt = claimExpiresAt.Value;
                if (!string.Equals(checkTime.ClaimStatus, "paid", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(checkTime.ClaimStatus, "waived", StringComparison.OrdinalIgnoreCase))
                {
                    checkTime.ClaimStatus = "locked";
                }

                if (!tenantRefuted && feeTotal > 0m)
                {
                    var status = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime);
                    if (status != FeeSettlementStatusPaid && status != FeeSettlementStatusWaived)
                    {
                        checkTime.FeeSettlementStatus = FeeSettlementStatusDue;
                        checkTime.FeeDueAt ??= claimExpiresAt.Value;
                    }
                }

                changed = true;
                await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, null, "claim_locked", new { ClaimExpiresAt = claimExpiresAt.Value });
            }

            if (!checkTime.ActualCheckIn.HasValue && !checkTime.NoShowMarkedAt.HasValue)
            {
                var noShowEligibleAt = checkTime.ScheduledCheckIn.AddHours(noShowGraceHours);
                var bookingOpenForCheckIn = string.Equals(booking.Status, BookingStatus.confirmed.ToString(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(booking.Status, BookingStatus.paid.ToString(), StringComparison.OrdinalIgnoreCase);
                if (bookingOpenForCheckIn && now >= noShowEligibleAt)
                {
                    checkTime.NoShowStatus = "auto_confirmed";
                    checkTime.NoShowMarkedBy = Guid.Empty;
                    checkTime.NoShowMarkedAt = now;
                    checkTime.ClaimStatus = "no_show_confirmed";
                    checkTime.ClaimOpenedAt ??= now;
                    checkTime.ClaimExpiresAt ??= now;
                    checkTime.ClaimLockedAt ??= now;

                    booking.Status = BookingStatus.cancelled.ToString();
                    _bookingRepository.Update(booking);
                    await _bookingRepository.SaveChangesAsync();
                    await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);
                    changed = true;
                    await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, null, "no_show_auto_confirmed", new { NoShowMarkedAt = now });
                }
            }

            if (checkTime.ActualCheckIn.HasValue && !checkTime.ActualCheckOut.HasValue)
            {
                var missingCheckOutEligibleAt = checkTime.ScheduledCheckOut.AddHours(missingCheckOutGraceHours);
                if (now >= missingCheckOutEligibleAt)
                {
                    checkTime.ActualCheckOut = checkTime.ScheduledCheckOut;
                    checkTime.IsLateCheckOut = false;
                    checkTime.LateCheckOutFee = 0m;
                    checkTime.MissingCheckOutStatus = "auto_closed";
                    checkTime.AutoClosedAt = now;
                    checkTime.ClaimOpenedAt ??= now;
                    checkTime.ClaimExpiresAt ??= now;
                    checkTime.ClaimLockedAt ??= now;
                    checkTime.ClaimStatus ??= "closed";
                    checkTime.UpdatedAt = now;

                    ApplyFeeSettlementState(checkTime, GetCheckTimeFeeTotal(checkTime), now);

                    booking.Status = BookingStatus.completed.ToString();
                    _bookingRepository.Update(booking);
                    await _bookingRepository.SaveChangesAsync();
                    await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);
                    changed = true;
                    await CreateCheckTimeStateEventAsync(booking.BookingId, checkTime.CheckTimeId, null, "missing_checkout_auto_closed", new { AutoClosedAt = now });
                }
            }

            if (changed)
            {
                _bookingCheckTimeRepository.Update(checkTime);
                await _bookingCheckTimeRepository.SaveChangesAsync();
                processed++;
            }
        }

        // Emit automation run completed event
        await CreateCheckTimeStateEventAsync(Guid.Empty, null, null, "automation_run_completed", new { RunId = runId, CompletedAt = Common.Utils.VietnamTime.Now, Processed = processed });
    }

    private async Task EnsureTenantHasNoOutstandingCheckTimeFeesAsync(Guid tenantId)
    {
        var bookings = await _bookingRepository.FindAsync(b => b.TenantId == tenantId);
        var bookingIds = bookings.Select(b => b.BookingId).ToList();
        if (!bookingIds.Any())
        {
            return;
        }

        var now = Common.Utils.VietnamTime.Now;
        var checkTimes = await _bookingCheckTimeRepository.FindAsync(ct => bookingIds.Contains(ct.BookingId));
        var outstandingFees = checkTimes
            .Where(ct => IsFeeSettlementRequired(ct, now))
            .ToList();

        if (!outstandingFees.Any())
        {
            return;
        }

        var totalOutstanding = outstandingFees.Sum(GetCheckTimeFeeTotal);
        throw new InvalidOperationException($"You have unpaid check-time fees totaling {totalOutstanding:0.00}. Please settle them before creating a new booking.");
    }

    private async Task CreateCheckTimeStateEventAsync(Guid bookingId, Guid? checkTimeId, Guid? actorId, string eventType, object? eventData = null)
    {
        try
        {
            if (_checkTimeStateEventRepository == null)
            {
                return;
            }

            string? serializedEventData = null;
            if (eventData != null)
            {
                serializedEventData = JsonSerializer.Serialize(eventData);
            }

            var ev = new BookingCheckTimeStateEvent
            {
                EventId = Guid.NewGuid(),
                BookingId = bookingId,
                CheckTimeId = checkTimeId,
                EventType = eventType,
                EventData = serializedEventData,
                CreatedBy = actorId,
                CreatedAt = Common.Utils.VietnamTime.Now
            };

            await _checkTimeStateEventRepository.AddAsync(ev);
            await _checkTimeStateEventRepository.SaveChangesAsync();
        }
        catch
        {
            // swallow errors to avoid breaking main flow; consider logging
        }
    }

    private async Task EnsureTenantHasNoPendingCheckTimeRequestsAsync(Guid tenantId)
    {
        if (_checkTimeRequestRepository == null)
        {
            return; // CheckTimeRequest feature not enabled
        }

        var bookings = await _bookingRepository.FindAsync(b => b.TenantId == tenantId);
        var bookingIds = bookings.Select(b => b.BookingId).ToList();

        if (!bookingIds.Any())
        {
            return;
        }

        // Check if any booking has pending or counter-offered check-time requests
        foreach (var bookingId in bookingIds)
        {
            var hasPending = await _checkTimeRequestRepository.HasPendingOrCounterOfferAsync(bookingId, "EarlyCheckIn") ||
                             await _checkTimeRequestRepository.HasPendingOrCounterOfferAsync(bookingId, "LateCheckOut");

            if (hasPending)
            {
                throw new InvalidOperationException("You have pending check-time requests that must be resolved before creating a new booking.");
            }
        }
    }

    private static decimal GetCheckTimeFeeTotal(BookingCheckTime checkTime)
    {
        return (checkTime.EarlyCheckInFee ?? 0m) + (checkTime.LateCheckOutFee ?? 0m);
    }

    private void ApplyFeeSettlementState(BookingCheckTime checkTime, decimal feeAmount, DateTime now)
    {
        var currentStatus = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime);
        if (string.Equals(currentStatus, FeeSettlementStatusDisputed, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (feeAmount <= 0m)
        {
            checkTime.FeeSettlementStatus = FeeSettlementStatusNone;
            checkTime.FeeDueAt = null;
            checkTime.FeeSettledAt = null;
            checkTime.FeeSettlementNotes = null;
            return;
        }

        checkTime.FeeSettlementStatus = FeeSettlementStatusDue;
        checkTime.FeeDueAt ??= now.AddDays(GetFeeSettlementGraceDays());
        checkTime.FeeSettledAt = null;
    }

    private async Task EnsureActorIsOwnerOrStaffAsync(Guid actorId, Guid landlordId)
    {
        // Allow system/automation actor (Guid.Empty) to perform actions
        if (actorId == Guid.Empty)
            return;

        var user = await _userRepository.GetByIdAsync(actorId);
        if (user == null)
            throw new InvalidOperationException("Actor not found.");

        var role = (user.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (role == "landlord")
        {
            if (landlordId != actorId)
                throw new InvalidOperationException("You are not allowed to modify bookings for properties you do not own.");
            return;
        }

        if (role == "tenant")
        {
            throw new InvalidOperationException("Tenants are not allowed to perform this action.");
        }

        // staff/admin/other roles are allowed by default
    }

    private async Task EnsureIsStaffOrAdminAsync(Guid actorId)
    {
        if (actorId == Guid.Empty)
            return;

        var user = await _userRepository.GetByIdAsync(actorId);
        if (user == null)
            throw new InvalidOperationException("Actor not found.");

        var role = (user.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (role != "staff" && role != "admin")
            throw new InvalidOperationException("Only staff or admin users can perform this action.");
    }

    private static string NormalizeFeeSettlementStatus(string? feeSettlementStatus, BookingCheckTime checkTime)
    {
        if (!string.IsNullOrWhiteSpace(feeSettlementStatus))
        {
            return feeSettlementStatus.Trim().ToLowerInvariant();
        }

        return GetCheckTimeFeeTotal(checkTime) > 0m ? FeeSettlementStatusDue : FeeSettlementStatusNone;
    }

    private static bool IsFeeSettlementRequired(BookingCheckTime checkTime, DateTime now)
    {
        var status = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime);
        if (status == FeeSettlementStatusPaid || status == FeeSettlementStatusWaived || status == FeeSettlementStatusNone)
        {
            return false;
        }

        if (status == FeeSettlementStatusDisputed)
        {
            return false;
        }

        if (GetCheckTimeFeeTotal(checkTime) <= 0m)
        {
            return false;
        }

        var tenantRefuted = string.Equals(checkTime.TenantResponseStatus, "refuted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checkTime.TenantResponseStatus, "disputed", StringComparison.OrdinalIgnoreCase);
        var claimExpiresAt = checkTime.ClaimExpiresAt ?? checkTime.ClaimOpenedAt?.AddHours(24) ?? checkTime.UpdatedAt?.AddHours(24);
        var claimExpiredUnrefuted = claimExpiresAt.HasValue && now >= claimExpiresAt.Value && !tenantRefuted;
        if (claimExpiredUnrefuted)
        {
            return true;
        }

        if (!checkTime.FeeDueAt.HasValue)
        {
            return true;
        }

        return checkTime.FeeDueAt.Value <= now;
    }

    private int GetFeeSettlementGraceDays()
    {
        var configuredDays = _configuration.GetValue<int>("BookingCheckTimeSettings:FeeSettlementGraceDays", 3);
        return configuredDays <= 0 ? 3 : configuredDays;
    }

    /// <summary>
    /// Retrieves the availability calendar for an apartment, showing available and unavailable date ranges.
    /// Anonymous users see availability only; landlord/staff see blocking details including booking IDs.
    /// </summary>
    public async Task<AvailabilityCalendarResponseDto> GetAvailabilityCalendarAsync(
        Guid apartmentId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? requesterId = null,
        string? requesterRole = null)
    {
        // Validate apartment exists and is posted
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        if (!string.Equals(apartment.Status, "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This apartment is not currently available for booking.");

        await ExpireUnpaidBookingsIfOverdueAsync(apartmentId);

        // Set default date range: today to 90 days from today
        var calendarStartDate = startDate ?? Common.Utils.VietnamTime.Now.Date;
        var calendarEndDate = endDate ?? Common.Utils.VietnamTime.Now.AddDays(90).Date;

        // Validate date range
        if (calendarStartDate >= calendarEndDate)
            throw new ArgumentException("Start date must be before end date.");

        var rangeDays = (calendarEndDate - calendarStartDate).Days;
        if (rangeDays > 365)
            throw new ArgumentException("Date range cannot exceed 365 days.");

        // Add one day to end date to include the entire end date (exclusive check-out date logic)
        calendarEndDate = calendarEndDate.AddDays(1);

        // Fetch blocking bookings (those that prevent booking)
        var blockingStatuses = new[] { "negotiating", "confirmed", "paid", "completed", "disputed" };
        var bookings = await _bookingRepository.FindAsync(b =>
            b.ApartmentId == apartmentId &&
            b.Status != null &&
            blockingStatuses.Contains(b.Status) &&
            b.CheckInDate < DateOnly.FromDateTime(calendarEndDate) &&
            b.CheckOutDate > DateOnly.FromDateTime(calendarStartDate));

        var blackoutRules = await _apartmentAvailabilityRepository.FindAsync(r =>
            r.ApartmentId == apartmentId &&
            r.StartDate < DateOnly.FromDateTime(calendarEndDate) &&
            r.EndDate > DateOnly.FromDateTime(calendarStartDate));

        // Fetch price calendar entries for pricing information
        var priceCalendars = await _apartmentPriceCalendarRepository.FindAsync(c =>
            c.ApartmentId == apartmentId &&
            c.StartDate < DateOnly.FromDateTime(calendarEndDate) &&
            c.EndDate > DateOnly.FromDateTime(calendarStartDate));

        // Build unavailable periods from bookings
        var unavailablePeriods = new List<DateRangeBlockingDto>();
        bool isLandlordOwner = requesterId.HasValue && string.Equals(requesterRole, "landlord", StringComparison.OrdinalIgnoreCase)
            && apartment.LandlordId == requesterId;

        foreach (var booking in bookings.OrderBy(b => b.CheckInDate))
        {
            var unavailStart = booking.CheckInDate.ToDateTime(TimeOnly.MinValue);
            var unavailEnd = booking.CheckOutDate.ToDateTime(TimeOnly.MinValue);

            var blockingDto = new DateRangeBlockingDto
            {
                StartDate = unavailStart,
                EndDate = unavailEnd,
                Reason = $"Booking {booking.Status}",
                BookingId = isLandlordOwner ? booking.BookingId : null,
                BookingStatus = isLandlordOwner ? booking.Status : null
            };
            unavailablePeriods.Add(blockingDto);
        }

        foreach (var rule in blackoutRules.OrderBy(r => r.StartDate))
        {
            unavailablePeriods.Add(new DateRangeBlockingDto
            {
                StartDate = rule.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = rule.EndDate.ToDateTime(TimeOnly.MinValue),
                Reason = string.IsNullOrWhiteSpace(rule.Reason) ? "Unavailable by landlord" : rule.Reason,
                BookingId = null,
                BookingStatus = null
            });
        }

        // Merge overlapping unavailable periods
        var mergedUnavailable = MergeUnavailablePeriods(unavailablePeriods);

        // Build available periods and assign pricing
        var availablePeriods = BuildAvailablePeriods(
            calendarStartDate,
            calendarEndDate,
            mergedUnavailable,
            priceCalendars.ToList(),
            apartment.BasePricePerNight);

        var confirmedReservationStatuses = new[] { "confirmed", "paid", "completed", "disputed" };
        var calendarBookingStatus = ComputeRangeBookingStatus(
            apartment.Status,
            blackoutRules.Any(),
            bookings.Any(b => b.Status != null && confirmedReservationStatuses.Contains(b.Status, StringComparer.OrdinalIgnoreCase)));

        return new AvailabilityCalendarResponseDto
        {
            ApartmentId = apartmentId,
            BookingStatus = calendarBookingStatus,
            CalendarStartDate = calendarStartDate,
            CalendarEndDate = calendarEndDate.AddDays(-1), // Return original end date (without the +1 day)
            GeneratedAt = Common.Utils.VietnamTime.Now,
            AvailablePeriods = availablePeriods,
            UnavailablePeriods = mergedUnavailable
        };
    }

    /// <summary>
    /// Merges overlapping unavailable periods into consolidated date ranges.
    /// </summary>
    private List<DateRangeBlockingDto> MergeUnavailablePeriods(List<DateRangeBlockingDto> periods)
    {
        if (!periods.Any())
            return new List<DateRangeBlockingDto>();

        var sorted = periods.OrderBy(p => p.StartDate).ToList();
        var merged = new List<DateRangeBlockingDto> { sorted[0] };

        foreach (var period in sorted.Skip(1))
        {
            var lastMerged = merged.Last();

            // If periods overlap or are adjacent, merge them
            if (period.StartDate <= lastMerged.EndDate)
            {
                lastMerged.EndDate = period.EndDate > lastMerged.EndDate ? period.EndDate : lastMerged.EndDate;

                if (lastMerged.BookingId != period.BookingId)
                {
                    lastMerged.BookingId = null;
                }

                if (!string.Equals(lastMerged.BookingStatus, period.BookingStatus, StringComparison.OrdinalIgnoreCase))
                {
                    lastMerged.BookingStatus = null;
                }

                if (!string.Equals(lastMerged.Reason, period.Reason, StringComparison.OrdinalIgnoreCase))
                {
                    lastMerged.Reason = "Unavailable";
                }
            }
            else
            {
                merged.Add(period);
            }
        }

        return merged;
    }

    /// <summary>
    /// Builds available periods by filling gaps between unavailable periods,
    /// and assigns pricing information from price calendars.
    /// </summary>
    private List<DateRangePriceDto> BuildAvailablePeriods(
        DateTime calendarStart,
        DateTime calendarEnd,
        List<DateRangeBlockingDto> unavailablePeriods,
        List<DAL.Models.ApartmentPriceCalendar> priceCalendars,
        decimal defaultPrice)
    {
        var availablePeriods = new List<DateRangePriceDto>();

        if (!unavailablePeriods.Any())
        {
            // Entire period is available
            var price = priceCalendars.Any()
                ? priceCalendars.Average(p => p.DiscountPercentage.HasValue && p.IsDiscount == true
                    ? defaultPrice * (1 - p.DiscountPercentage.Value / 100)
                    : defaultPrice)
                : (decimal?)null;

            availablePeriods.Add(new DateRangePriceDto
            {
                StartDate = calendarStart,
                EndDate = calendarEnd,
                PricePerNight = price
            });
            return availablePeriods;
        }

        var sortedUnavailable = unavailablePeriods.OrderBy(p => p.StartDate).ToList();

        // Check if there's availability before the first unavailable period
        if (calendarStart < sortedUnavailable[0].StartDate)
        {
            var availEnd = sortedUnavailable[0].StartDate;
            var price = GetPriceForRange(calendarStart, availEnd, priceCalendars, defaultPrice);
            availablePeriods.Add(new DateRangePriceDto
            {
                StartDate = calendarStart,
                EndDate = availEnd,
                PricePerNight = price
            });
        }

        // Check for gaps between unavailable periods
        for (int i = 0; i < sortedUnavailable.Count - 1; i++)
        {
            var gapStart = sortedUnavailable[i].EndDate;
            var gapEnd = sortedUnavailable[i + 1].StartDate;

            if (gapStart < gapEnd)
            {
                var price = GetPriceForRange(gapStart, gapEnd, priceCalendars, defaultPrice);
                availablePeriods.Add(new DateRangePriceDto
                {
                    StartDate = gapStart,
                    EndDate = gapEnd,
                    PricePerNight = price
                });
            }
        }

        // Check if there's availability after the last unavailable period
        if (sortedUnavailable.Last().EndDate < calendarEnd)
        {
            var availStart = sortedUnavailable.Last().EndDate;
            var price = GetPriceForRange(availStart, calendarEnd, priceCalendars, defaultPrice);
            availablePeriods.Add(new DateRangePriceDto
            {
                StartDate = availStart,
                EndDate = calendarEnd,
                PricePerNight = price
            });
        }

        return availablePeriods;
    }

    /// <summary>
    /// Determines the price per night for a given date range based on price calendars.
    /// Returns null if no matching price calendar entry is found.
    /// </summary>
    private decimal? GetPriceForRange(
        DateTime rangeStart,
        DateTime rangeEnd,
        List<DAL.Models.ApartmentPriceCalendar> priceCalendars,
        decimal defaultPrice)
    {
        // Find price calendars that overlap with this range
        var overlappingCalendars = priceCalendars.Where(c =>
            c.StartDate <= DateOnly.FromDateTime(rangeEnd) &&
            c.EndDate >= DateOnly.FromDateTime(rangeStart)).ToList();

        if (!overlappingCalendars.Any())
            return null;

        var manualOverride = overlappingCalendars
            .Where(c =>
                c.PriceType != null &&
                c.FixedPricePerNight.HasValue)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (manualOverride != null)
        {
            return manualOverride.FixedPricePerNight!.Value;
        }

        var firstCalendar = overlappingCalendars
            .Where(c => c.IsDiscount == true && c.DiscountPercentage.HasValue)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (firstCalendar != null)
        {
            return defaultPrice * (1 - firstCalendar.DiscountPercentage!.Value / 100);
        }

        return defaultPrice;
    }

    private decimal CalculateBaseAmountForRange(
        DateOnly checkInDate,
        DateOnly checkOutDate,
        decimal defaultPrice,
        List<DAL.Models.ApartmentPriceCalendar> calendars)
    {
        decimal total = 0m;
        var cursor = checkInDate;

        while (cursor < checkOutDate)
        {
            total += ResolveNightlyPrice(cursor, defaultPrice, calendars);
            cursor = cursor.AddDays(1);
        }

        return total;
    }

    private List<DailyPriceResolutionDto> BuildPriceCalendarBreakdown(
        DateOnly checkInDate,
        DateOnly checkOutDate,
        decimal defaultPrice,
        List<DAL.Models.ApartmentPriceCalendar> calendars)
    {
        var priceCalendar = new List<DailyPriceResolutionDto>();
        var cursor = checkInDate;

        while (cursor < checkOutDate)
        {
            var (nightlyRate, source) = ResolveNightlyPriceDetails(cursor, defaultPrice, calendars);
            priceCalendar.Add(new DailyPriceResolutionDto
            {
                Date = cursor,
                FinalPricePerNight = Math.Round(nightlyRate, 2, MidpointRounding.AwayFromZero),
                TotalNightlyCost = Math.Round(nightlyRate, 2, MidpointRounding.AwayFromZero),
                Source = source,
                Notes = $"Rate calculated based on {source} rule."
            });

            cursor = cursor.AddDays(1);
        }

        return priceCalendar;
    }

    private decimal ResolveNightlyPrice(
        DateOnly date,
        decimal defaultPrice,
        List<DAL.Models.ApartmentPriceCalendar> calendars)
    {
        return ResolveNightlyPriceDetails(date, defaultPrice, calendars).Price;
    }

    private (decimal Price, string Source) ResolveNightlyPriceDetails(
        DateOnly date,
        decimal defaultPrice,
        List<DAL.Models.ApartmentPriceCalendar> calendars)
    {
        var matching = calendars.Where(c => c.StartDate <= date && c.EndDate >= date).ToList();

        if (!matching.Any())
        {
            return (defaultPrice, "BaseRate");
        }

        var manualOverride = matching
            .Where(c =>
                c.PriceType != null &&
                c.FixedPricePerNight.HasValue)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (manualOverride != null)
        {
            return (
                manualOverride.FixedPricePerNight!.Value,
                string.IsNullOrWhiteSpace(manualOverride.PriceType)
                    ? "ManualOverride"
                    : manualOverride.PriceType.Replace('_', ' '));
        }

        var discountCalendar = matching
            .Where(c => c.IsDiscount == true && c.DiscountPercentage.HasValue)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (discountCalendar != null)
        {
            return (
                defaultPrice * (1 - discountCalendar.DiscountPercentage!.Value / 100m),
                string.IsNullOrWhiteSpace(discountCalendar.PriceType)
                    ? "CalendarDiscount"
                    : discountCalendar.PriceType.Replace('_', ' '));
        }

        var fallbackCalendar = matching
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (fallbackCalendar?.FixedPricePerNight.HasValue == true)
        {
            return (
                fallbackCalendar.FixedPricePerNight.Value,
                string.IsNullOrWhiteSpace(fallbackCalendar.PriceType)
                    ? "CalendarPeriod"
                    : fallbackCalendar.PriceType.Replace('_', ' '));
        }

        return (defaultPrice, "BaseRate");
    }

    public async Task<SetApartmentAvailabilityResponseDto> SetApartmentAvailabilityAsync(
        Guid apartmentId,
        Guid landlordId,
        SetApartmentAvailabilityRequestDto dto)
    {
        if (dto.Ranges == null || dto.Ranges.Count == 0)
            throw new ArgumentException("At least one availability range is required.");

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            throw new KeyNotFoundException("Apartment not found.");

        if (apartment.LandlordId != landlordId)
            throw new KeyNotFoundException("Apartment not found.");

        var normalizedIncoming = new List<(DateOnly StartDate, DateOnly EndDate, string? Reason)>();
        foreach (var range in dto.Ranges)
        {
            var start = DateOnly.FromDateTime(range.StartDate.Date);
            var end = DateOnly.FromDateTime(range.EndDate.Date);

            if (start >= end)
                throw new ArgumentException("Each range must have start date earlier than end date.");

            normalizedIncoming.Add((start, end, range.Reason));
        }

        var minStart = normalizedIncoming.Min(r => r.StartDate);
        var maxEnd = normalizedIncoming.Max(r => r.EndDate);

        var existingOverlaps = (await _apartmentAvailabilityRepository.FindAsync(r =>
            r.ApartmentId == apartmentId &&
            r.StartDate < maxEnd &&
            r.EndDate > minStart)).ToList();

        var allRanges = existingOverlaps
            .Select(r => (r.StartDate, r.EndDate, r.Reason))
            .Concat(normalizedIncoming)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.EndDate)
            .ToList();

        var merged = new List<(DateOnly StartDate, DateOnly EndDate, string? Reason)>();
        foreach (var current in allRanges)
        {
            if (merged.Count == 0)
            {
                merged.Add(current);
                continue;
            }

            var last = merged[^1];
            if (current.StartDate <= last.EndDate)
            {
                var newEnd = current.EndDate > last.EndDate ? current.EndDate : last.EndDate;
                var newReason = string.IsNullOrWhiteSpace(last.Reason) ? current.Reason : last.Reason;
                merged[^1] = (last.StartDate, newEnd, newReason);
            }
            else
            {
                merged.Add(current);
            }
        }

        foreach (var rule in existingOverlaps)
        {
            _apartmentAvailabilityRepository.Remove(rule);
        }

        foreach (var item in merged)
        {
            await _apartmentAvailabilityRepository.AddAsync(new ApartmentAvailability
            {
                AvailabilityId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Reason = item.Reason,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            });
        }

        await _apartmentAvailabilityRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(apartmentId);

        return new SetApartmentAvailabilityResponseDto
        {
            ApartmentId = apartmentId,
            SubmittedRanges = dto.Ranges.Count,
            AppliedRanges = merged.Count,
            MergedRanges = merged.Select(m => new AvailabilityAppliedRangeDto
            {
                StartDate = m.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = m.EndDate.ToDateTime(TimeOnly.MinValue),
                Reason = m.Reason
            }).ToList()
        };
    }

    public async Task<RemoveApartmentAvailabilityResponseDto> RemoveApartmentAvailabilityAsync(
        Guid apartmentId,
        Guid landlordId,
        RemoveApartmentAvailabilityRequestDto dto)
    {
        if (dto.Ranges == null || dto.Ranges.Count == 0)
            throw new ArgumentException("At least one availability range is required.");

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            throw new KeyNotFoundException("Apartment not found.");

        if (apartment.LandlordId != landlordId)
            throw new KeyNotFoundException("Apartment not found.");

        var removalRanges = new List<(DateOnly StartDate, DateOnly EndDate)>();
        foreach (var range in dto.Ranges)
        {
            var start = DateOnly.FromDateTime(range.StartDate.Date);
            var end = DateOnly.FromDateTime(range.EndDate.Date);

            if (start >= end)
                throw new ArgumentException("Each range must have start date earlier than end date.");

            removalRanges.Add((start, end));
        }

        var mergedRemovals = MergeRemovalRanges(removalRanges);
        var minStart = mergedRemovals.Min(r => r.StartDate);
        var maxEnd = mergedRemovals.Max(r => r.EndDate);

        var existingOverlaps = (await _apartmentAvailabilityRepository.FindAsync(r =>
            r.ApartmentId == apartmentId &&
            r.StartDate < maxEnd &&
            r.EndDate > minStart)).ToList();

        if (!existingOverlaps.Any())
        {
            return new RemoveApartmentAvailabilityResponseDto
            {
                ApartmentId = apartmentId,
                SubmittedRanges = dto.Ranges.Count,
                AffectedRules = 0,
                RemainingRanges = 0
            };
        }

        var remainingSegments = new List<(DateOnly StartDate, DateOnly EndDate, string? Reason)>();
        foreach (var rule in existingOverlaps)
        {
            var segments = new List<(DateOnly StartDate, DateOnly EndDate)> { (rule.StartDate, rule.EndDate) };

            foreach (var removal in mergedRemovals)
            {
                var nextSegments = new List<(DateOnly StartDate, DateOnly EndDate)>();
                foreach (var segment in segments)
                {
                    if (removal.EndDate <= segment.StartDate || removal.StartDate >= segment.EndDate)
                    {
                        nextSegments.Add(segment);
                        continue;
                    }

                    if (segment.StartDate < removal.StartDate)
                    {
                        nextSegments.Add((segment.StartDate, removal.StartDate));
                    }

                    if (removal.EndDate < segment.EndDate)
                    {
                        nextSegments.Add((removal.EndDate, segment.EndDate));
                    }
                }

                segments = nextSegments;
                if (!segments.Any())
                {
                    break;
                }
            }

            foreach (var segment in segments)
            {
                if (segment.StartDate < segment.EndDate)
                {
                    remainingSegments.Add((segment.StartDate, segment.EndDate, rule.Reason));
                }
            }
        }

        foreach (var rule in existingOverlaps)
        {
            _apartmentAvailabilityRepository.Remove(rule);
        }

        foreach (var segment in remainingSegments.OrderBy(s => s.StartDate).ThenBy(s => s.EndDate))
        {
            await _apartmentAvailabilityRepository.AddAsync(new ApartmentAvailability
            {
                AvailabilityId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = segment.StartDate,
                EndDate = segment.EndDate,
                Reason = segment.Reason,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            });
        }

        await _apartmentAvailabilityRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(apartmentId);

        return new RemoveApartmentAvailabilityResponseDto
        {
            ApartmentId = apartmentId,
            SubmittedRanges = dto.Ranges.Count,
            AffectedRules = existingOverlaps.Count,
            RemainingRanges = remainingSegments.Count,
            UpdatedRanges = remainingSegments
                .OrderBy(s => s.StartDate)
                .ThenBy(s => s.EndDate)
                .Select(s => new AvailabilityAppliedRangeDto
                {
                    StartDate = s.StartDate.ToDateTime(TimeOnly.MinValue),
                    EndDate = s.EndDate.ToDateTime(TimeOnly.MinValue),
                    Reason = s.Reason
                })
                .ToList()
        };
    }

    private static List<(DateOnly StartDate, DateOnly EndDate)> MergeRemovalRanges(
        List<(DateOnly StartDate, DateOnly EndDate)> ranges)
    {
        if (!ranges.Any())
            return new List<(DateOnly StartDate, DateOnly EndDate)>();

        var sorted = ranges.OrderBy(r => r.StartDate).ThenBy(r => r.EndDate).ToList();
        var merged = new List<(DateOnly StartDate, DateOnly EndDate)> { sorted[0] };

        foreach (var current in sorted.Skip(1))
        {
            var last = merged[^1];
            if (current.StartDate <= last.EndDate)
            {
                var end = current.EndDate > last.EndDate ? current.EndDate : last.EndDate;
                merged[^1] = (last.StartDate, end);
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static bool IsApartmentListingLocked(string? apartmentStatus)
    {
        return string.Equals(apartmentStatus, "draft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apartmentStatus, "pending_review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apartmentStatus, "blocked", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apartmentStatus, "archived", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExpireUnpaidBookingsIfOverdueAsync(Guid apartmentId)
    {
        var nowUtc = Common.Utils.VietnamTime.Now;
        var today = DateOnly.FromDateTime(nowUtc.Date);
        var pendingHoldHours = _configuration.GetValue<int>("BookingPaymentSettings:PendingHoldHours", 24);
        var pendingCutoff = nowUtc.AddHours(-pendingHoldHours);

        var candidates = (await _bookingRepository.FindAsync(b =>
            b.ApartmentId == apartmentId &&
            b.Status != null &&
            (b.Status == "pending" || b.Status == "confirmed"))).ToList();

        if (!candidates.Any())
            return;

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        var changedBookings = new List<Booking>();

        foreach (var booking in candidates)
        {
            if (string.Equals(booking.Status, "pending", StringComparison.OrdinalIgnoreCase) && booking.DepositPaid != true)
            {
                if (!booking.CreatedAt.HasValue || booking.CreatedAt.Value <= pendingCutoff)
                {
                    booking.Status = "cancelled";
                    _bookingRepository.Update(booking);
                    changedBookings.Add(booking);
                }

                continue;
            }

            if (string.Equals(booking.Status, "confirmed", StringComparison.OrdinalIgnoreCase) && booking.BalanceDueDate < today)
            {
                if (booking.DepositPaid == true)
                {
                    await RefundBookingAsync(
                        booking.BookingId,
                        booking.TenantId,
                        new RequestBookingRefundDto
                        {
                            Reason = "system_cancellation",
                            Notes = "Booking cancelled due to payment timeout."
                        });
                }
                else
                {
                    booking.Status = "cancelled";
                    _bookingRepository.Update(booking);
                    changedBookings.Add(booking);
                }
            }
        }

        if (!changedBookings.Any())
            return;

        await _bookingRepository.SaveChangesAsync();

        if (apartment == null)
            return;

        foreach (var booking in changedBookings)
        {
            await CreateBookingNotificationAsync(
                booking.TenantId,
                NotificationType.booking_cancelled.ToString(),
                "Booking cancelled",
                $"Your unpaid booking for apartment '{apartment.Title}' has been cancelled due to payment timeout.",
                booking.BookingId);

            await CreateBookingNotificationAsync(
                apartment.LandlordId,
                NotificationType.booking_cancelled.ToString(),
                "Booking cancelled",
                $"An unpaid booking for apartment '{apartment.Title}' was cancelled due to payment timeout.",
                booking.BookingId);
        }
    }

    private static string ComputeRangeBookingStatus(
        string? apartmentStatus,
        bool hasBlackoutOverlap,
        bool hasConfirmedReservationOverlap)
    {
        if (IsApartmentListingLocked(apartmentStatus) || hasBlackoutOverlap)
            return "locked";

        if (hasConfirmedReservationOverlap)
            return "confirmed";

        return "available";
    }

    public async Task<IReadOnlyList<OccupiedRoomAlternativeOptionDto>> FindAlternativeApartmentsAsync(Guid bookingId, int maxResults = 5, int? radiusMeters = null)
    {
        if (maxResults <= 0)
            throw new ArgumentException("maxResults must be greater than zero.");

        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        var sourceApartment = await _apartmentRepository.GetApartmentWithDetailsAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        var occupantCount = (booking.NoOfAdults ?? 0) + (booking.NoOfChildren ?? 0);
        if (occupantCount <= 0)
        {
            occupantCount = 1;
        }

        var hasPets = (booking.NoOfPets ?? 0) > 0;
        var tolerance = _configuration.GetValue<decimal>("OccupiedRoomAlternatives:PriceTolerancePercent", DefaultPriceTolerancePercent);
        if (tolerance < 0)
        {
            tolerance = DefaultPriceTolerancePercent;
        }

        var sourceCity = NormalizeLocationValue(sourceApartment.City);
        var sourceDistrict = NormalizeLocationValue(sourceApartment.District);
        if (sourceCity == null || sourceDistrict == null)
        {
            return Array.Empty<OccupiedRoomAlternativeOptionDto>();
        }

        var candidateApartments = await _apartmentRepository.FindAsync(a =>
            a.ApartmentId != booking.ApartmentId &&
            a.Status == "posted" &&
            a.City != null && a.City.Trim().ToUpper() == sourceCity &&
            a.District != null && a.District.Trim().ToUpper() == sourceDistrict &&
            (!hasPets || a.IsPetAllowed == true) &&
            (!a.MaxOccupants.HasValue || (int)a.MaxOccupants >= occupantCount) &&
            (!a.MaxInfants.HasValue || (booking.NoOfInfants ?? 0) <= a.MaxInfants.Value));

        var alternatives = new List<OccupiedRoomAlternativeOptionDto>();
        foreach (var candidate in candidateApartments)
        {
            try
            {
                await EnsureNoConflictingBookingsAsync(candidate.ApartmentId, booking.CheckInDate, booking.CheckOutDate);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var alternativeTotal = EstimateAlternativeTotalPrice(candidate, booking);
            if (!IsWithinPriceTolerance(booking.TotalPrice, alternativeTotal, tolerance))
            {
                continue;
            }

            var detailedApartment = await _apartmentRepository.GetApartmentWithDetailsAsync(candidate.ApartmentId);
            if (detailedApartment == null)
            {
                continue;
            }

            alternatives.Add(new OccupiedRoomAlternativeOptionDto
            {
                ApartmentId = detailedApartment.ApartmentId,
                ApartmentTitle = detailedApartment.Title,
                BasePricePerNight = detailedApartment.BasePricePerNight,
                EstimatedTotalPrice = alternativeTotal,
                PriceDifference = Math.Round(alternativeTotal - booking.TotalPrice, 2, MidpointRounding.AwayFromZero),
                AdjustmentType = GetAdjustmentType(alternativeTotal - booking.TotalPrice),
                Apartment = _mapper.Map<ApartmentResponseDto>(detailedApartment)
            });
        }

        return alternatives
            .OrderBy(o => Math.Abs(o.PriceDifference))
            .ThenBy(o => o.EstimatedTotalPrice)
            .Take(maxResults)
            .ToList();
    }

    public async Task<BookingOfferResponseDto> CreateAlternativeOfferAsync(
        Guid bookingId,
        Guid alternativeApartmentId,
        Guid? staffUserId,
        string? reason = null,
        int? expiresInHours = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        if (booking.ApartmentId == alternativeApartmentId)
            throw new InvalidOperationException("Alternative apartment must be different from the original booking apartment.");

        var alternativeApartment = await _apartmentRepository.GetApartmentWithDetailsAsync(alternativeApartmentId)
            ?? throw new ArgumentException("Alternative apartment not found.");

        if (!string.Equals(alternativeApartment.Status, "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Alternative apartment is not available for booking.");

        var sourceApartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Original apartment not found.");

        if (!string.Equals(NormalizeLocationValue(sourceApartment.City), NormalizeLocationValue(alternativeApartment.City), StringComparison.Ordinal)
            || !string.Equals(NormalizeLocationValue(sourceApartment.District), NormalizeLocationValue(alternativeApartment.District), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Alternative apartment must be in the same city and district as the original booking.");
        }

        var occupantCount = (booking.NoOfAdults ?? 0) + (booking.NoOfChildren ?? 0);
        if (occupantCount <= 0)
        {
            occupantCount = 1;
        }

        if (alternativeApartment.MaxOccupants.HasValue && (int)alternativeApartment.MaxOccupants < occupantCount)
        {
            throw new InvalidOperationException("Alternative apartment does not satisfy occupancy requirements.");
        }

        if (alternativeApartment.MaxInfants.HasValue && (booking.NoOfInfants ?? 0) > alternativeApartment.MaxInfants.Value)
        {
            throw new InvalidOperationException("Alternative apartment does not satisfy infant capacity requirements.");
        }

        if ((booking.NoOfPets ?? 0) > 0 && alternativeApartment.IsPetAllowed != true)
        {
            throw new InvalidOperationException("Alternative apartment does not allow pets for this booking.");
        }

        await EnsureNoConflictingBookingsAsync(alternativeApartmentId, booking.CheckInDate, booking.CheckOutDate);

        var now = Common.Utils.VietnamTime.Now;
        var existingPendingOffers = await _bookingOfferRepository.GetPendingOffersByBookingAsync(bookingId, now);
        if (existingPendingOffers.Any(o => o.AlternativeApartmentId == alternativeApartmentId))
        {
            throw new InvalidOperationException("A pending offer already exists for this alternative apartment.");
        }

        var alternativePrice = EstimateAlternativeTotalPrice(alternativeApartment, booking);
        var priceDifference = Math.Round(alternativePrice - booking.TotalPrice, 2, MidpointRounding.AwayFromZero);
        var expiryHours = expiresInHours.GetValueOrDefault(
            _configuration.GetValue<int>("OccupiedRoomAlternatives:OfferExpiryHours", DefaultOfferExpiryHours));
        if (expiryHours <= 0)
        {
            expiryHours = DefaultOfferExpiryHours;
        }

        var offer = new BookingOffer
        {
            OfferId = Guid.NewGuid(),
            OriginalBookingId = bookingId,
            AlternativeApartmentId = alternativeApartmentId,
            TenantId = booking.TenantId,
            CreatedByStaffId = staffUserId,
            OriginalPrice = booking.TotalPrice,
            AlternativePrice = alternativePrice,
            PriceDifference = priceDifference,
            Status = "pending",
            Reason = string.IsNullOrWhiteSpace(reason) ? "room_occupied" : reason,
            ExpiresAt = now.AddHours(expiryHours),
            CreatedAt = now
        };

        await _bookingOfferRepository.AddAsync(offer);
        await _bookingOfferRepository.SaveChangesAsync();

        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.system_announcement.ToString(),
            "Alternative apartment offer available",
            "An alternative apartment option was prepared for your booking issue. Please review and respond before it expires.",
            booking.BookingId);

        await CreateBookingNotificationAsync(
            sourceApartment.LandlordId,
            NotificationType.system_announcement.ToString(),
            "Occupancy incident offer created",
            staffUserId.HasValue
                ? "Support staff created an alternative apartment offer for a tenant due to occupancy incident."
                : "An alternative apartment offer was automatically created for a tenant after an occupancy incident report.",
            booking.BookingId);

        // Audit event for alternative offer creation (staff action)
        try
        {
            await CreateCheckTimeStateEventAsync(booking.BookingId, null, staffUserId, "alternative_offer_created", new { OfferId = offer.OfferId, AlternativeApartmentId = alternativeApartmentId, CreatedAt = now, Reason = offer.Reason });
        }
        catch
        {
            // swallow audit failures
        }

        var createdOffer = await _bookingOfferRepository.GetOfferWithDetailsAsync(offer.OfferId)
            ?? throw new InvalidOperationException("Offer created but failed to load details.");

        return MapOfferToResponse(createdOffer);
    }

    public async Task<IReadOnlyList<BookingOfferResponseDto>> GetTenantActiveOffersAsync(Guid tenantId)
    {
        var now = Common.Utils.VietnamTime.Now;
        var offers = await _bookingOfferRepository.GetPendingOffersForTenantAsync(tenantId, now);
        return offers.Select(MapOfferToResponse).ToList();
    }

    public async Task<BookingOfferResponseDto> RespondToAlternativeOfferAsync(Guid offerId, Guid tenantId, bool accepted, string? notes = null)
    {
        var now = Common.Utils.VietnamTime.Now;
        var offer = await _bookingOfferRepository.GetOfferWithDetailsAsync(offerId)
            ?? throw new KeyNotFoundException("Offer not found.");

        if (offer.TenantId != tenantId)
            throw new InvalidOperationException("This offer does not belong to the current tenant.");

        if (!string.Equals(offer.Status, "pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only pending offers can be responded to.");

        if (offer.ExpiresAt.HasValue && offer.ExpiresAt.Value <= now)
        {
            offer.Status = "expired";
            offer.RespondedAt = now;
            _bookingOfferRepository.Update(offer);
            await _bookingOfferRepository.SaveChangesAsync();
            throw new InvalidOperationException("Offer has expired.");
        }

        offer.Status = accepted ? "accepted" : "rejected";
        offer.RespondedAt = now;
        offer.TenantResponseNotes = notes;
        _bookingOfferRepository.Update(offer);

        if (accepted)
        {
            var siblings = await _bookingOfferRepository.GetPendingOffersByBookingAsync(offer.OriginalBookingId, now);
            foreach (var sibling in siblings.Where(o => o.OfferId != offer.OfferId))
            {
                sibling.Status = "cancelled";
                sibling.RespondedAt = now;
                sibling.TenantResponseNotes = "Automatically cancelled after another offer was accepted.";
                _bookingOfferRepository.Update(sibling);
            }
        }
        else
        {
            try
            {
                var alternatives = await FindAlternativeApartmentsAsync(offer.OriginalBookingId, 1);
                var nextAlternative = alternatives.FirstOrDefault();

                if (nextAlternative != null)
                {
                    await CreateAlternativeOfferAsync(
                        offer.OriginalBookingId,
                        nextAlternative.ApartmentId,
                        null,
                        "room_occupied_offer_after_rejection");
                }
            }
            catch (InvalidOperationException)
            {
                // Silently continue: keep rejection successful even if next offer cannot be created.
            }
            catch (ArgumentException)
            {
                // Silently continue: keep rejection successful even if next offer cannot be created.
            }
        }

        await _bookingOfferRepository.SaveChangesAsync();

        await CreateBookingNotificationAsync(
            tenantId,
            NotificationType.system_announcement.ToString(),
            accepted ? "Alternative offer accepted" : "Alternative offer rejected",
            accepted
                ? "Your response was recorded. Staff will complete manual settlement and booking adjustment."
                : "Your rejection was recorded. Staff will follow up with additional options.",
            offer.OriginalBookingId);

        var updatedOffer = await _bookingOfferRepository.GetOfferWithDetailsAsync(offerId)
            ?? throw new InvalidOperationException("Offer updated but failed to reload details.");

        return MapOfferToResponse(updatedOffer);
    }

    private decimal EstimateAlternativeTotalPrice(Apartment alternativeApartment, Booking originalBooking)
    {
        var nights = originalBooking.Nights;
        if (nights <= 0)
        {
            nights = originalBooking.CheckOutDate.DayNumber - originalBooking.CheckInDate.DayNumber;
        }

        if (nights <= 0)
        {
            nights = 1;
        }

        return Math.Round(alternativeApartment.BasePricePerNight * nights, 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsWithinPriceTolerance(decimal originalPrice, decimal alternativePrice, decimal tolerancePercent)
    {
        if (originalPrice <= 0)
            return true;

        var min = originalPrice * (1 - tolerancePercent);
        var max = originalPrice * (1 + tolerancePercent);
        return alternativePrice >= min && alternativePrice <= max;
    }

    private static string GetAdjustmentType(decimal priceDifference)
    {
        if (priceDifference > 0)
            return "upgrade";
        if (priceDifference < 0)
            return "downgrade";
        return "same_price";
    }

    private static string? NormalizeLocationValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    private BookingOfferResponseDto MapOfferToResponse(BookingOffer offer)
    {
        return new BookingOfferResponseDto
        {
            OfferId = offer.OfferId,
            OriginalBookingId = offer.OriginalBookingId,
            AlternativeApartmentId = offer.AlternativeApartmentId,
            Status = offer.Status,
            Reason = offer.Reason,
            OriginalPrice = offer.OriginalPrice,
            AlternativePrice = offer.AlternativePrice,
            PriceDifference = offer.PriceDifference,
            AdjustmentType = GetAdjustmentType(offer.PriceDifference),
            ManualSettlementRequired = true,
            ExpiresAt = offer.ExpiresAt,
            RespondedAt = offer.RespondedAt,
            TenantResponseNotes = offer.TenantResponseNotes,
            AlternativeApartment = _mapper.Map<ApartmentResponseDto>(offer.AlternativeApartment)
        };
    }

    private async Task RefreshApartmentBookingStatusSnapshotAsync(Guid apartmentId)
    {
        await ExpireUnpaidBookingsIfOverdueAsync(apartmentId);

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            return;

        var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now.Date);

        var hasBlackoutOverlap = (await _apartmentAvailabilityRepository.FindAsync(a =>
            a.ApartmentId == apartmentId &&
            a.EndDate > today)).Any();

        var confirmedReservationStatuses = new[] { "confirmed", "paid", "completed", "disputed" };
        var hasConfirmedReservationOverlap = (await _bookingRepository.FindAsync(b =>
            b.ApartmentId == apartmentId &&
            b.Status != null &&
            confirmedReservationStatuses.Contains(b.Status) &&
            b.CheckOutDate > today)).Any();

        var computed = ComputeRangeBookingStatus(apartment.Status, hasBlackoutOverlap, hasConfirmedReservationOverlap);
        if (!string.Equals(apartment.BookingStatus, computed, StringComparison.OrdinalIgnoreCase))
        {
            apartment.BookingStatus = computed;
            _apartmentRepository.Update(apartment);
            await _apartmentRepository.SaveChangesAsync();
        }
    }

    public async Task<OutstandingCheckTimeFeesResponseDto> GetOutstandingCheckTimeFeesAsync(Guid userId, Guid? requesterId = null, string? requesterRole = null)
    {
        // Authorization: tenants can only see their own fees; staff/admin can see any user's fees
        if (!string.Equals(requesterRole, "staff", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requesterRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!requesterId.HasValue || requesterId.Value != userId)
            {
                throw new InvalidOperationException("You are not authorized to view outstanding fees for this user.");
            }
        }

        // Get all bookings for the user
        var userBookings = await _bookingRepository.FindAsync(b => b.TenantId == userId);
        var bookingIds = userBookings.Select(b => b.BookingId).ToList();

        if (!bookingIds.Any())
        {
            return new OutstandingCheckTimeFeesResponseDto
            {
                UserId = userId,
                TotalOutstandingFees = 0m,
                TotalOutstandingCount = 0,
                OverdueCount = 0,
                DisputedCount = 0,
                OutstandingFees = new()
            };
        }

        // Get all check-time records for these bookings
        var checkTimes = await _bookingCheckTimeRepository.FindAsync(ct => bookingIds.Contains(ct.BookingId));
        var now = Common.Utils.VietnamTime.Now;

        var outstandingFees = new List<OutstandingCheckTimeFeeItemDto>();
        decimal totalOutstanding = 0m;
        int overdueCount = 0;
        int disputedCount = 0;

        foreach (var checkTime in checkTimes)
        {
            var feeTotal = GetCheckTimeFeeTotal(checkTime);
            var status = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime);

            // Include if: not paid, not waived, and has fees
            if (feeTotal > 0m && status != FeeSettlementStatusPaid && status != FeeSettlementStatusWaived)
            {
                var booking = userBookings.FirstOrDefault(b => b.BookingId == checkTime.BookingId);
                if (booking == null) continue;

                var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
                var isOverdue = IsFeeSettlementRequired(checkTime, now);

                if (isOverdue) overdueCount++;
                if (string.Equals(status, FeeSettlementStatusDisputed, StringComparison.OrdinalIgnoreCase)) disputedCount++;

                var feeItem = new OutstandingCheckTimeFeeItemDto
                {
                    BookingId = checkTime.BookingId,
                    ApartmentId = booking.ApartmentId,
                    ApartmentAddress = apartment?.Address,
                    ScheduledCheckIn = checkTime.ScheduledCheckIn,
                    ScheduledCheckOut = checkTime.ScheduledCheckOut,
                    EarlyCheckInFee = checkTime.EarlyCheckInFee ?? 0m,
                    LateCheckOutFee = checkTime.LateCheckOutFee ?? 0m,
                    TotalFee = feeTotal,
                    FeeSettlementStatus = status,
                    FeeDueAt = checkTime.FeeDueAt,
                    FeeSettledAt = checkTime.FeeSettledAt,
                    IsOverdue = isOverdue,
                    TenantDisputeReason = checkTime.TenantDisputeReason,
                    DisputeResolutionStatus = checkTime.DisputeResolutionStatus
                };

                outstandingFees.Add(feeItem);
                totalOutstanding += feeTotal;
            }
        }

        // Sort by FeeDueAt (earliest first), nulls last
        outstandingFees = outstandingFees
            .OrderBy(f => f.FeeDueAt ?? DateTime.MaxValue)
            .ToList();

        return new OutstandingCheckTimeFeesResponseDto
        {
            UserId = userId,
            TotalOutstandingFees = Math.Round(totalOutstanding, 2, MidpointRounding.AwayFromZero),
            TotalOutstandingCount = outstandingFees.Count,
            OverdueCount = overdueCount,
            DisputedCount = disputedCount,
            OutstandingFees = outstandingFees
        };
    }

    public async Task<LandlordOutstandingCheckTimeFeesResponseDto> GetLandlordOutstandingCheckTimeFeesAsync(Guid landlordId, Guid? requesterId = null, string? requesterRole = null)
    {
        // Authorization: landlords can only see their own fees; staff/admin can see any landlord's fees
        if (!string.Equals(requesterRole, "staff", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requesterRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!requesterId.HasValue || requesterId.Value != landlordId)
            {
                throw new InvalidOperationException("You are not authorized to view outstanding fees for this landlord.");
            }
        }

        // Get all apartments for the landlord
        var apartments = await _apartmentRepository.FindAsync(a => a.LandlordId == landlordId);
        var apartmentIds = apartments.Select(a => a.ApartmentId).ToList();

        if (!apartmentIds.Any())
        {
            return new LandlordOutstandingCheckTimeFeesResponseDto
            {
                LandlordId = landlordId,
                TotalOutstandingFees = 0m,
                TotalOutstandingCount = 0,
                OverdueCount = 0,
                DisputedCount = 0,
                UniqueTenantCount = 0,
                WalletPenaltyTotalAmount = 0m,
                WalletPenaltyCount = 0,
                OutstandingFees = new(),
                WalletPenaltyTransactions = new()
            };
        }

        // Get all bookings for these apartments
        var bookings = await _bookingRepository.FindAsync(b => apartmentIds.Contains(b.ApartmentId));
        var bookingIds = bookings.Select(b => b.BookingId).ToList();

        if (!bookingIds.Any())
        {
            return new LandlordOutstandingCheckTimeFeesResponseDto
            {
                LandlordId = landlordId,
                TotalOutstandingFees = 0m,
                TotalOutstandingCount = 0,
                OverdueCount = 0,
                DisputedCount = 0,
                UniqueTenantCount = 0,
                WalletPenaltyTotalAmount = 0m,
                WalletPenaltyCount = 0,
                OutstandingFees = new(),
                WalletPenaltyTransactions = new()
            };
        }

        // Get all check-time records for these bookings
        var checkTimes = await _bookingCheckTimeRepository.FindAsync(ct => bookingIds.Contains(ct.BookingId));
        var walletPenaltyPayments = (await _paymentRepository.FindAsync(payment =>
            payment.RelatedEntityType == PaymentRelatedEntityType.booking.ToString() &&
            payment.RelatedEntityId.HasValue &&
            payment.Method == "landlord_wallet_penalty" &&
            payment.Status == PaymentStatus.success.ToString())).ToList();
        var now = Common.Utils.VietnamTime.Now;

        var outstandingFees = new List<OutstandingCheckTimeFeeByTenantDto>();
        var walletPenaltyTransactions = new List<LandlordWalletPenaltyTransactionDto>();
        decimal totalOutstanding = 0m;
        int overdueCount = 0;
        int disputedCount = 0;
        var uniqueTenants = new HashSet<Guid>();

        foreach (var checkTime in checkTimes)
        {
            var feeTotal = GetCheckTimeFeeTotal(checkTime);
            var status = NormalizeFeeSettlementStatus(checkTime.FeeSettlementStatus, checkTime);

            // Include if: not paid, not waived, and has fees
            if (feeTotal > 0m && status != FeeSettlementStatusPaid && status != FeeSettlementStatusWaived)
            {
                var booking = bookings.FirstOrDefault(b => b.BookingId == checkTime.BookingId);
                if (booking == null) continue;

                var apartment = apartments.FirstOrDefault(a => a.ApartmentId == booking.ApartmentId);
                var tenant = await _userRepository.GetByIdAsync(booking.TenantId);
                var isOverdue = IsFeeSettlementRequired(checkTime, now);

                if (isOverdue) overdueCount++;
                if (string.Equals(status, FeeSettlementStatusDisputed, StringComparison.OrdinalIgnoreCase)) disputedCount++;

                uniqueTenants.Add(booking.TenantId);

                var feeItem = new OutstandingCheckTimeFeeByTenantDto
                {
                    BookingId = checkTime.BookingId,
                    ApartmentId = booking.ApartmentId,
                    ApartmentAddress = apartment?.Address,
                    TenantId = booking.TenantId,
                    TenantName = tenant?.FullName,
                    ScheduledCheckIn = checkTime.ScheduledCheckIn,
                    ScheduledCheckOut = checkTime.ScheduledCheckOut,
                    EarlyCheckInFee = checkTime.EarlyCheckInFee ?? 0m,
                    LateCheckOutFee = checkTime.LateCheckOutFee ?? 0m,
                    TotalFee = feeTotal,
                    FeeSettlementStatus = status,
                    FeeDueAt = checkTime.FeeDueAt,
                    FeeSettledAt = checkTime.FeeSettledAt,
                    IsOverdue = isOverdue,
                    TenantDisputeReason = checkTime.TenantDisputeReason,
                    DisputeResolutionStatus = checkTime.DisputeResolutionStatus
                };

                outstandingFees.Add(feeItem);
                totalOutstanding += feeTotal;
            }
        }

        foreach (var payment in walletPenaltyPayments)
        {
            var bookingId = payment.RelatedEntityId!.Value;
            var booking = bookings.FirstOrDefault(b => b.BookingId == bookingId);
            if (booking == null)
            {
                continue;
            }

            var apartment = apartments.FirstOrDefault(a => a.ApartmentId == booking.ApartmentId);
            if (apartment == null || apartment.LandlordId != landlordId)
            {
                continue;
            }

            var tenant = await _userRepository.GetByIdAsync(booking.TenantId);
            walletPenaltyTransactions.Add(new LandlordWalletPenaltyTransactionDto
            {
                PaymentId = payment.PaymentId,
                BookingId = booking.BookingId,
                ApartmentId = booking.ApartmentId,
                ApartmentAddress = apartment.Address,
                TenantId = booking.TenantId,
                TenantName = tenant?.FullName,
                Amount = payment.Amount,
                PaymentType = payment.PaymentType,
                PaymentPurpose = payment.PaymentPurpose,
                Method = payment.Method,
                Status = payment.Status,
                TransactionId = payment.TransactionId,
                PaidAt = payment.PaidAt
            });
        }

        // Sort by FeeDueAt (earliest first), nulls last
        outstandingFees = outstandingFees
            .OrderBy(f => f.FeeDueAt ?? DateTime.MaxValue)
            .ToList();

        walletPenaltyTransactions = walletPenaltyTransactions
            .OrderByDescending(p => p.PaidAt ?? DateTime.MinValue)
            .ThenByDescending(p => p.PaymentId)
            .ToList();

        return new LandlordOutstandingCheckTimeFeesResponseDto
        {
            LandlordId = landlordId,
            TotalOutstandingFees = Math.Round(totalOutstanding, 2, MidpointRounding.AwayFromZero),
            TotalOutstandingCount = outstandingFees.Count,
            OverdueCount = overdueCount,
            DisputedCount = disputedCount,
            UniqueTenantCount = uniqueTenants.Count,
            WalletPenaltyTotalAmount = Math.Round(walletPenaltyTransactions.Sum(p => p.Amount), 2, MidpointRounding.AwayFromZero),
            WalletPenaltyCount = walletPenaltyTransactions.Count,
            OutstandingFees = outstandingFees,
            WalletPenaltyTransactions = walletPenaltyTransactions
        };
    }

    public async Task<(IEnumerable<ReportedBookingDto> Items, int TotalCount)> GetReportedBookingsAsync(
        int page = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var reportedBookingsList = await BuildReportedBookingsListAsync();

        // Apply filtering
        if (!string.IsNullOrWhiteSpace(search))
        {
            reportedBookingsList = reportedBookingsList
                .Where(b => b.TenantFullName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                           b.LandlordFullName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                           b.ApartmentAddress?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        if (fromDate.HasValue)
        {
            reportedBookingsList = reportedBookingsList
                .Where(b => b.CreatedAt >= fromDate)
                .ToList();
        }

        if (toDate.HasValue)
        {
            reportedBookingsList = reportedBookingsList
                .Where(b => b.CreatedAt <= toDate)
                .ToList();
        }

        // Apply sorting
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            sortBy = "CreatedAt";
        }

        var isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        reportedBookingsList = sortBy.ToLower() switch
        {
            "tenantname" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.TenantFullName).ToList()
                : reportedBookingsList.OrderBy(b => b.TenantFullName).ToList(),
            "landlordname" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.LandlordFullName).ToList()
                : reportedBookingsList.OrderBy(b => b.LandlordFullName).ToList(),
            "price" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.TotalPrice).ToList()
                : reportedBookingsList.OrderBy(b => b.TotalPrice).ToList(),
            "checkinddate" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.CheckInDate).ToList()
                : reportedBookingsList.OrderBy(b => b.CheckInDate).ToList(),
            _ => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.CreatedAt).ToList()
                : reportedBookingsList.OrderBy(b => b.CreatedAt).ToList()
        };

        var totalCount = reportedBookingsList.Count;
        var items = reportedBookingsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<(IEnumerable<ReportedBookingDto> Items, int TotalCount)> GetDisputedBookingsAsync(
        int page = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var reportedBookingsList = (await BuildReportedBookingsListAsync())
            .Where(b => b.HasCheckTimeDispute)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            reportedBookingsList = reportedBookingsList
                .Where(b => b.TenantFullName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                           b.LandlordFullName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                           b.ApartmentAddress?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        if (fromDate.HasValue)
        {
            reportedBookingsList = reportedBookingsList
                .Where(b => b.CreatedAt >= fromDate)
                .ToList();
        }

        if (toDate.HasValue)
        {
            reportedBookingsList = reportedBookingsList
                .Where(b => b.CreatedAt <= toDate)
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(sortBy))
        {
            sortBy = "CreatedAt";
        }

        var isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        reportedBookingsList = sortBy.ToLower() switch
        {
            "tenantname" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.TenantFullName).ToList()
                : reportedBookingsList.OrderBy(b => b.TenantFullName).ToList(),
            "landlordname" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.LandlordFullName).ToList()
                : reportedBookingsList.OrderBy(b => b.LandlordFullName).ToList(),
            "price" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.TotalPrice).ToList()
                : reportedBookingsList.OrderBy(b => b.TotalPrice).ToList(),
            "checkinddate" => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.CheckInDate).ToList()
                : reportedBookingsList.OrderBy(b => b.CheckInDate).ToList(),
            _ => isDescending
                ? reportedBookingsList.OrderByDescending(b => b.CreatedAt).ToList()
                : reportedBookingsList.OrderBy(b => b.CreatedAt).ToList()
        };

        var totalCount = reportedBookingsList.Count;
        var items = reportedBookingsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    private async Task<List<ReportedBookingDto>> BuildReportedBookingsListAsync()
    {
        // Get all bookings with disputed status or bookings that have support tickets
        var allBookings = await _bookingRepository.FindAsync(b => b.Status == "disputed");

        // Get bookings with support tickets from tenants
        var supportTickets = (await _supportTicketRepository.FindWithAttachmentsNoTrackingAsync(t =>
    t.Category == "booking_issue" || t.Category == "dispute")).ToList();

        var bookingIdsWithTickets = supportTickets
            .Where(t => t.BookingId.HasValue)
            .Select(t => t.BookingId!.Value)
            .ToHashSet();

        foreach (var ticket in supportTickets.Where(t => !t.BookingId.HasValue))
        {
            var legacyBookingId = ExtractBookingIdFromSupportTicket(ticket);
            if (legacyBookingId.HasValue)
            {
                bookingIdsWithTickets.Add(legacyBookingId.Value);
            }
        }

        var reportedBookings = allBookings.ToList();

        // Get booking check-time records to find disputes
        var checkTimes = await _bookingCheckTimeRepository.FindAsync(ct =>
            !string.IsNullOrEmpty(ct.DisputeResolutionStatus) || ct.TenantResponseStatus == "dispute" || ct.TenantResponseStatus == "refuted");

        var bookingIdsWithCheckTimeDisputes = checkTimes.Select(ct => ct.BookingId).Distinct().ToList();

        var allReportedBookingIds = new HashSet<Guid>();
        foreach (var booking in reportedBookings)
        {
            allReportedBookingIds.Add(booking.BookingId);
        }
        foreach (var id in bookingIdsWithCheckTimeDisputes)
        {
            allReportedBookingIds.Add(id);
        }
        foreach (var id in bookingIdsWithTickets)
        {
            allReportedBookingIds.Add(id);
        }

        var reportedBookingsList = new List<ReportedBookingDto>();

        foreach (var bookingId in allReportedBookingIds)
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null) continue;

            var checkTime = checkTimes.FirstOrDefault(ct => ct.BookingId == bookingId);
            var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
            var tenant = await _userRepository.GetByIdAsync(booking.TenantId);
            var landlord = apartment != null ? await _userRepository.GetByIdAsync(apartment.LandlordId) : null;

            var ticketsForBooking = supportTickets
                .Where(t => t.BookingId == bookingId
                    || (!t.BookingId.HasValue && ExtractBookingIdFromSupportTicket(t) == bookingId))
                .OrderByDescending(t => t.CreatedAt ?? DateTime.MinValue)
                .ToList();

            var supportTicketImageUrls = ticketsForBooking
                .SelectMany(t => t.SupportTicketAttachments ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a.FileUrl))
                .Select(a => a.FileUrl)
                .Distinct()
                .ToList();

            var latestTicket = ticketsForBooking.FirstOrDefault();

            if (latestTicket != null)
            {
                var loadedTicket = await _supportTicketRepository.GetByIdAsync(latestTicket.TicketId);                

                if (loadedTicket?.SupportTicketAttachments == null)
                {
                    continue;
                }

                supportTicketImageUrls.AddRange(
                    loadedTicket.SupportTicketAttachments
                        .Where(a => !string.IsNullOrWhiteSpace(a.FileUrl))
                        .Select(a => a.FileUrl));
            }

            List<string> checkTimeImageUrls = new();
            if (checkTime != null)
            {
                if (!string.IsNullOrWhiteSpace(checkTime.CheckInPhotoUrl))
                {
                    checkTimeImageUrls.Add(checkTime.CheckInPhotoUrl);
                }

                if (!string.IsNullOrWhiteSpace(checkTime.CheckOutPhotoUrl))
                {
                    checkTimeImageUrls.Add(checkTime.CheckOutPhotoUrl);
                }
            }

            supportTicketImageUrls = supportTicketImageUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct()
                .ToList();

            checkTimeImageUrls = checkTimeImageUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct()
                .ToList();

            var reportedDto = new ReportedBookingDto
            {
                BookingId = booking.BookingId,
                TenantId = booking.TenantId,
                TenantFullName = tenant?.FullName,
                ApartmentId = booking.ApartmentId,
                ApartmentAddress = apartment?.Address,
                LandlordId = apartment?.LandlordId ?? Guid.Empty,
                LandlordFullName = landlord?.FullName,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                Nights = booking.Nights,
                TotalPrice = booking.TotalPrice,
                BookingStatus = booking.Status,
                HasCheckTimeDispute = checkTime != null &&
                    (checkTime.TenantResponseStatus == "dispute" ||
                     checkTime.TenantResponseStatus == "refuted" ||
                     !string.IsNullOrEmpty(checkTime.DisputeResolutionStatus)),
                DisputeReason = checkTime?.TenantDisputeReason,
                DisputeResolutionStatus = checkTime?.DisputeResolutionStatus,
                DisputeCreatedAt = checkTime?.TenantRespondedAt,
                SupportTicketCount = ticketsForBooking.Count,
                TicketId = latestTicket?.TicketId,
                Images = supportTicketImageUrls,
                CheckTimeImages = checkTimeImageUrls,
                CreatedAt = booking.CreatedAt
            };

            reportedBookingsList.Add(reportedDto);
        }

        return reportedBookingsList;
    }

    private static Guid? ExtractBookingIdFromSupportTicket(SupportTicket ticket)
    {
        static Guid? TryExtractGuid(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(
                text,
                @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
                RegexOptions.CultureInvariant);

            return match.Success && Guid.TryParse(match.Value, out var bookingId)
                ? bookingId
                : null;
        }

        return TryExtractGuid(ticket.Subject)
            ?? TryExtractGuid(ticket.Description);
    }
}

