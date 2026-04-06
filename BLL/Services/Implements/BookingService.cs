using BLL.Services.Interfaces;
using AutoMapper;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;
using NotificationType = Common.Enums.Notification;
using ApartmentBookingStatusEnum = Common.Enums.ApartmentBookingStatus;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BLL.Services.Implements;

public class BookingService : BaseService<Booking>, IBookingService
{
    private const decimal SuggestedDepositRate = 0.30m;
    private const int DefaultOfferExpiryHours = 2;
    private const decimal DefaultPriceTolerancePercent = 0.20m;

    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingOfferRepository _bookingOfferRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IApartmentPriceCalendarRepository _apartmentPriceCalendarRepository;
    private readonly IRepository<Package> _packageRepository;
    private readonly IRepository<DAL.Models.Notification> _notificationRepository;
    private readonly IRepository<BookingCheckTime> _bookingCheckTimeRepository;
    private readonly IRepository<TemporaryResidenceReport> _temporaryResidenceReportRepository;
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<ApartmentAvailability> _apartmentAvailabilityRepository;
    private readonly IRepository<SupportTicket> _supportTicketRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IIdentityVerificationService _identityVerificationService;
    private readonly ILandlordWalletService _landlordWalletService;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;

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
            IRepository<SupportTicket> supportTicketRepository,
            IRepository<Payment> paymentRepository,
            IIdentityVerificationService identityVerificationService,
            ILandlordWalletService landlordWalletService,
            IConfiguration configuration,
            IMapper mapper) : base(repository)
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
        _identityVerificationService = identityVerificationService;
        _landlordWalletService = landlordWalletService;
        _configuration = configuration;
        _mapper = mapper;
    }

    public async Task<ConfirmOccupiedIncidentPenaltyResponseDto> ConfirmOccupiedIncidentPenaltyAsync(
        Guid bookingId,
        Guid confirmedBy,
        Guid? ticketId = null,
        string? notes = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        if (booking.DepositAmount <= 0)
            throw new InvalidOperationException("Booking deposit amount is not valid for penalty calculation.");

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        var penaltyTransactionId = $"occupied_penalty_{bookingId:N}";
        var existingPenalty = (await _paymentRepository.FindAsync(p => p.TransactionId == penaltyTransactionId)).FirstOrDefault();
        if (existingPenalty != null)
        {
            return new ConfirmOccupiedIncidentPenaltyResponseDto
            {
                BookingId = booking.BookingId,
                TicketId = ticketId,
                PenaltyAmount = booking.DepositAmount,
                AlreadyApplied = true,
                Message = "Penalty was already applied for this occupied incident.",
                Settlement = new LandlordPenaltyApplicationResultDto
                {
                    RequestedAmount = booking.DepositAmount,
                    DeductedFromAvailable = 0m,
                    DeductedFromPending = 0m,
                    DebtRecorded = 0m
                }
            };
        }

        var ticket = await ResolveOccupiedIncidentTicketAsync(bookingId, booking.TenantId, ticketId);
        if (ticket == null)
        {
            throw new InvalidOperationException("Occupied incident ticket was not found for this booking.");
        }

        var settlement = await _landlordWalletService.ApplyOccupiedIncidentPenaltyAsync(apartment.LandlordId, booking.DepositAmount);

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
            PaidAt = DateTime.UtcNow
        };

        await _paymentRepository.AddAsync(payment);

        ticket.Status = "resolved";
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.ResolvedBy = confirmedBy;
        var supportNotes = $"Occupied incident penalty applied. Amount: {booking.DepositAmount:0.00}. " +
                           $"From available: {settlement.DeductedFromAvailable:0.00}, " +
                           $"from pending: {settlement.DeductedFromPending:0.00}, " +
                           $"debt: {settlement.DebtRecorded:0.00}.";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            supportNotes += $" Staff notes: {notes}";
        }

        ticket.ResolutionNotes = string.IsNullOrWhiteSpace(ticket.ResolutionNotes)
            ? supportNotes
            : $"{ticket.ResolutionNotes}\n{supportNotes}";
        ticket.UpdatedAt = DateTime.UtcNow;
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

        return new ConfirmOccupiedIncidentPenaltyResponseDto
        {
            BookingId = booking.BookingId,
            TicketId = ticket.TicketId,
            PenaltyAmount = booking.DepositAmount,
            AlreadyApplied = false,
            Message = "Occupied incident penalty applied successfully.",
            Settlement = settlement
        };
    }

    private async Task<SupportTicket?> ResolveOccupiedIncidentTicketAsync(Guid bookingId, Guid tenantId, Guid? ticketId)
    {
        if (ticketId.HasValue)
        {
            var byId = await _supportTicketRepository.GetByIdAsync(ticketId.Value);
            if (byId == null)
            {
                return null;
            }

            if (!string.Equals(byId.Category, "booking_issue", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(byId.UserId.ToString(), tenantId.ToString(), StringComparison.OrdinalIgnoreCase)
                || !byId.Subject.Contains(bookingId.ToString(), StringComparison.OrdinalIgnoreCase)
                || !byId.Subject.Contains("occupied", StringComparison.OrdinalIgnoreCase))
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
            .Where(t => (t.Subject ?? string.Empty).Contains(subjectMarker, StringComparison.OrdinalIgnoreCase)
                        && (t.Subject ?? string.Empty).Contains("occupied", StringComparison.OrdinalIgnoreCase))
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
            CreatedAt = DateTime.UtcNow
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
        if (dto.CheckInDate >= dto.CheckOutDate)
            throw new ArgumentException("Check-out date must be later than check-in date.");

        var apartment = await _apartmentRepository.GetByIdAsync(dto.ApartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        if (!string.Equals(apartment.Status, "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This apartment is not currently available for booking.");

        if (string.Equals(apartment.BookingStatus, ApartmentBookingStatusEnum.Locked.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This apartment is currently locked for booking.");

        ValidateOccupancyLimits(
            apartment,
            dto.NoOfAdults,
            dto.NoOfPets);

        await EnsureNoConflictingBookingsAsync(dto.ApartmentId, dto.CheckInDate, dto.CheckOutDate);
        var nights = dto.CheckOutDate.DayNumber - dto.CheckInDate.DayNumber;

        // Enforce minimum stay based on apartment price calendar rules
        var calendars = await _apartmentPriceCalendarRepository.FindAsync(c =>
            c.ApartmentId == dto.ApartmentId &&
            c.StartDate <= dto.CheckOutDate &&
            c.EndDate >= dto.CheckInDate);

        if (calendars.Any())
        {
            var minRequiredNights = calendars.Max(c => c.MinNights ?? 1);
            if (nights < minRequiredNights)
            {
                throw new InvalidOperationException($"Booking must be at least {minRequiredNights} night(s) for the selected dates.");
            }
        }

        var baseAmount = apartment.BasePricePerNight * nights;

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
            BaseAmount = baseAmount,
            PackageAmount = packageAmount,
            TotalPrice = total,
            SuggestedDeposit = suggestedDeposit,
            RemainingBalance = remaining
        };
    }

    public async Task<Booking> CreateWithQuoteAsync(CreateBookingRequestDto requestDto, Guid tenantId)
    {
        await _identityVerificationService.EnsureUserVerifiedForBookingAsync(tenantId);

        var quote = await GetQuoteAsync(new BookingQuoteRequestDto
        {
            ApartmentId = requestDto.ApartmentId,
            PackageId = requestDto.PackageId,
            NoOfAdults = requestDto.NoOfAdults,
            NoOfInfants = requestDto.NoOfInfants,
            NoOfPets = requestDto.NoOfPets,
            CheckInDate = requestDto.CheckInDate,
            CheckOutDate = requestDto.CheckOutDate
        });

        var depositAmount = quote.SuggestedDeposit;
        if (depositAmount > quote.TotalPrice)
            throw new ArgumentException("Deposit cannot exceed total booking price.");

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            TenantId = tenantId,
            ApartmentId = requestDto.ApartmentId,
            CheckInDate = requestDto.CheckInDate,
            CheckOutDate = requestDto.CheckOutDate,
            Nights = quote.Nights,
            NoOfAdults = requestDto.NoOfAdults,
            NoOfInfants = requestDto.NoOfInfants,
            NoOfPets = requestDto.NoOfPets,
            TotalPrice = quote.TotalPrice,
            PackageId = requestDto.PackageId,
            PackagePrice = quote.PackageAmount,
            DepositAmount = depositAmount,
            DepositPaid = false,
            BalanceDueDate = requestDto.CheckInDate.AddDays(-1),
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        var checkTime = new BookingCheckTime
        {
            CheckTimeId = Guid.NewGuid(),
            BookingId = booking.BookingId,
            ScheduledCheckIn = booking.CheckInDate.ToDateTime(new TimeOnly(14, 0)),
            ScheduledCheckOut = booking.CheckOutDate.ToDateTime(new TimeOnly(12, 0)),
            TempResidenceReported = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

    private static void ValidateOccupancyLimits(
        Apartment apartment,
        int? requestedAdults,
        int? requestedPets)
    {
        var adults = requestedAdults ?? 0;
        if (apartment.MaxOccupants.HasValue && adults > apartment.MaxOccupants.Value)
        {
            throw new InvalidOperationException($"This apartment allows at most {apartment.MaxOccupants.Value} adult(s).");
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

    public async Task<Booking> MarkDepositPaidAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        if (booking.DepositPaid == true)
            return booking;

        await _identityVerificationService.EnsureUserVerifiedForBookingAsync(booking.TenantId);

        booking.DepositPaid = true;
        if (string.Equals(booking.Status, "pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "negotiating", StringComparison.OrdinalIgnoreCase))
        {
            booking.Status = "confirmed";
        }

        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            await _landlordWalletService.CreditPendingAsync(apartment.LandlordId, booking.DepositAmount);

            await CreateBookingNotificationAsync(
                apartment.LandlordId,
                NotificationType.booking_confirmed.ToString(),
                "New booking confirmed",
                $"A booking for apartment '{apartment.Title}' has been confirmed with deposit payment.",
                booking.BookingId);

            await CreateBookingNotificationAsync(
                booking.TenantId,
                NotificationType.booking_confirmed.ToString(),
                "Booking confirmed",
                $"Your booking for apartment '{apartment.Title}' has been confirmed after deposit payment.",
                booking.BookingId);
        }

        return booking;
    }

    public async Task<Booking> MarkBalancePaidAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        if (string.Equals(booking.Status, "paid", StringComparison.OrdinalIgnoreCase))
            return booking;

        if (booking.DepositPaid != true)
            throw new InvalidOperationException("Deposit must be paid before settling remaining balance.");

        booking.Status = "paid";
        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();
        await RefreshApartmentBookingStatusSnapshotAsync(booking.ApartmentId);

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            var remainingAmount = booking.TotalPrice - booking.DepositAmount;
            if (remainingAmount > 0)
            {
                await _landlordWalletService.CreditPendingAsync(apartment.LandlordId, remainingAmount);
            }

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
                $"Payment for booking at '{apartment.Title}' has been completed.",
                booking.BookingId);
        }

        return booking;
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
        return await _bookingRepository.GetByLandlordAsync(landlordId, page, pageSize, sortBy, sortOrder, search, fromDate, toDate);
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
        if (tenant == null || string.IsNullOrWhiteSpace(tenant.PassportId) || string.IsNullOrWhiteSpace(tenant.Nationality))
            throw new InvalidOperationException("Tenant passport and nationality are required before residence reporting.");

        var existingReport = (await _temporaryResidenceReportRepository.FindAsync(r => r.BookingId == bookingId)).FirstOrDefault();
        if (existingReport != null)
            throw new InvalidOperationException("Residence report has already been submitted for this booking.");

        var report = new TemporaryResidenceReport
        {
            ReportId = Guid.NewGuid(),
            BookingId = bookingId,
            LandlordId = landlordUserId,
            TenantPassportId = tenant.PassportId!,
            TenantNationality = tenant.Nationality!,
            CheckInDate = booking.CheckInDate,
            ReportedToPolice = dto.ReportedToPolice,
            ReportDate = dto.ReportDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ReportNumber = dto.ReportNumber
        };

        await _temporaryResidenceReportRepository.AddAsync(report);
        await _temporaryResidenceReportRepository.SaveChangesAsync();

        var checkTime = (await _bookingCheckTimeRepository.FindAsync(c => c.BookingId == bookingId)).FirstOrDefault();
        if (checkTime != null)
        {
            checkTime.TempResidenceReported = true;
            checkTime.ReportedAt = DateTime.UtcNow;
            checkTime.ReportReference = dto.ReportNumber;
            checkTime.RecordedBy = landlordUserId;
            checkTime.RecordedAt = DateTime.UtcNow;
            checkTime.UpdatedAt = DateTime.UtcNow;
            checkTime.ActualCheckIn ??= dto.ActualCheckIn ?? DateTime.UtcNow;

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

        return new TemporaryResidenceReportDetailsDto
        {
            ReportId = report.ReportId,
            BookingId = report.BookingId,
            LandlordId = report.LandlordId,
            TenantId = booking.TenantId,
            TenantFullName = tenantUser?.FullName,
            TenantPassportId = report.TenantPassportId,
            TenantNationality = report.TenantNationality,
            TenantPhone = tenantUser?.Phone,
            LandlordFullName = landlordUser?.FullName,
            LandlordPhone = landlordUser?.Phone,
            ApartmentTitle = apartment.Title,
            ApartmentAddress = apartment.Address,
            ApartmentDistrict = apartment.District,
            ApartmentCity = apartment.City,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            ReportedToPolice = report.ReportedToPolice,
            ReportDate = report.ReportDate,
            ReportNumber = report.ReportNumber
        };
    }

    private async Task EnsureNoConflictingBookingsAsync(Guid apartmentId, DateOnly checkInDate, DateOnly checkOutDate)
    {
        var blockingStatuses = new[] { "pending", "negotiating", "confirmed", "paid", "completed", "disputed" };

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

        var checkTime = await _bookingCheckTimeRepository.GetByIdAsync(bookingId) 
            ?? throw new KeyNotFoundException("Booking check-time record not found.");

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
            throw new KeyNotFoundException("Apartment not found.");

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
            var timeSinceRecording = DateTime.UtcNow - checkTime.RecordedAt.Value;
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
        checkTime.RecordedBy = recordedBy;
        checkTime.RecordedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            checkTime.Notes = dto.Notes;
        }

        _bookingCheckTimeRepository.Update(checkTime);
        await _bookingCheckTimeRepository.SaveChangesAsync();

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

        var checkTime = await _bookingCheckTimeRepository.GetByIdAsync(bookingId) 
            ?? throw new KeyNotFoundException("Booking check-time record not found.");

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
            throw new KeyNotFoundException("Apartment not found.");

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
                var timeSinceRecording = DateTime.UtcNow - recordedCheckOutTime.Value;
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
        checkTime.ActualCheckOut = dto.ActualCheckOut;
        checkTime.IsLateCheckOut = isLateCheckOut;
        checkTime.LateCheckOutFee = isLateCheckOut ? lateCheckOutFee : 0m;
        checkTime.UpdatedAt = DateTime.UtcNow;
        
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

        // Notify landlord
        var checkOutMessage = isLateCheckOut 
            ? $"Guest checked out late at {dto.ActualCheckOut:yyyy-MM-dd HH:mm}. Late check-out fee: ${lateCheckOutFee}" 
            : $"Guest checked out at {dto.ActualCheckOut:yyyy-MM-dd HH:mm} (on schedule).";
        
        await CreateBookingNotificationAsync(
            apartment.LandlordId,
            NotificationType.check_out_recorded.ToString(),
            "Check-out Recorded",
            checkOutMessage,
            bookingId);

        // Notify tenant
        await CreateBookingNotificationAsync(
            booking.TenantId,
            NotificationType.check_out_recorded.ToString(),
            "Check-out Recorded",
            $"Your checkout has been recorded. {(isLateCheckOut ? $"Late checkout fee: ${lateCheckOutFee}" : "Thank you for checking out on time!")}",
            bookingId);

        return await GetCheckTimeDetailsAsync(bookingId, apartment.LandlordId);
    }

    public async Task<BookingCheckTimeResponseDto> GetCheckTimeDetailsAsync(Guid bookingId, Guid? requesterId = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new KeyNotFoundException("Booking not found.");

        var checkTime = await _bookingCheckTimeRepository.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking check-time record not found.");

        // Calculate if still editable (within 24-hour window from RecordedAt)
        bool isEditable = false;
        if (checkTime.RecordedAt.HasValue)
        {
            var correctionWindowHours = _configuration.GetValue<int>("BookingCheckTimeSettings:CorrectionWindowHours", 24);
            var timeSinceRecording = DateTime.UtcNow - checkTime.RecordedAt.Value;
            isEditable = timeSinceRecording.TotalHours <= correctionWindowHours;
        }

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
            RecordedBy = checkTime.RecordedBy,
            RecordedAt = checkTime.RecordedAt,
            Notes = checkTime.Notes,
            LastModifiedAt = checkTime.UpdatedAt ?? checkTime.RecordedAt,
            IsEditable = isEditable
        };
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

        // Set default date range: today to 90 days from today
        var calendarStartDate = startDate ?? DateTime.UtcNow.Date;
        var calendarEndDate = endDate ?? DateTime.UtcNow.AddDays(90).Date;

        // Validate date range
        if (calendarStartDate >= calendarEndDate)
            throw new ArgumentException("Start date must be before end date.");

        var rangeDays = (calendarEndDate - calendarStartDate).Days;
        if (rangeDays > 365)
            throw new ArgumentException("Date range cannot exceed 365 days.");

        // Add one day to end date to include the entire end date (exclusive check-out date logic)
        calendarEndDate = calendarEndDate.AddDays(1);

        // Fetch blocking bookings (those that prevent booking)
        var blockingStatuses = new[] { "pending", "negotiating", "confirmed", "paid", "completed", "disputed" };
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
            GeneratedAt = DateTime.UtcNow,
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
            c.StartDate < DateOnly.FromDateTime(rangeEnd) &&
            c.EndDate > DateOnly.FromDateTime(rangeStart)).ToList();

        if (!overlappingCalendars.Any())
            return null;

        // If multiple overlapping calendars, use average or first
        // For now, using the first matching calendar's discount
        var firstCalendar = overlappingCalendars.First();
        if (firstCalendar.IsDiscount == true && firstCalendar.DiscountPercentage.HasValue)
        {
            return defaultPrice * (1 - firstCalendar.DiscountPercentage.Value / 100);
        }

        return null;
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
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

    private static string ComputeRangeBookingStatus(
        string? apartmentStatus,
        bool hasBlackoutOverlap,
        bool hasConfirmedReservationOverlap)
    {
        if (IsApartmentListingLocked(apartmentStatus) || hasBlackoutOverlap)
            return ApartmentBookingStatusEnum.Locked.ToString();

        if (hasConfirmedReservationOverlap)
            return ApartmentBookingStatusEnum.ConfirmedReservation.ToString();

        return ApartmentBookingStatusEnum.Available.ToString();
    }

    public async Task<IReadOnlyList<OccupiedRoomAlternativeOptionDto>> FindAlternativeApartmentsAsync(Guid bookingId, int maxResults = 5)
    {
        if (maxResults <= 0)
            throw new ArgumentException("maxResults must be greater than zero.");

        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new ArgumentException("Booking not found.");

        var sourceApartment = await _apartmentRepository.GetApartmentWithDetailsAsync(booking.ApartmentId)
            ?? throw new ArgumentException("Apartment not found for this booking.");

        var occupantCount = (booking.NoOfAdults ?? 0) + (booking.NoOfInfants ?? 0);
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

        var candidateApartments = await _apartmentRepository.FindAsync(a =>
            a.ApartmentId != booking.ApartmentId &&
            a.Status == "posted" &&
            a.City == sourceApartment.City &&
            a.District == sourceApartment.District &&
            (!hasPets || a.IsPetAllowed == true) &&
            (!a.MaxOccupants.HasValue || (int)a.MaxOccupants >= occupantCount));

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

        if (!string.Equals(sourceApartment.City, alternativeApartment.City, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sourceApartment.District, alternativeApartment.District, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Alternative apartment must be in the same city and district as the original booking.");
        }

        var occupantCount = (booking.NoOfAdults ?? 0) + (booking.NoOfInfants ?? 0);
        if (occupantCount <= 0)
        {
            occupantCount = 1;
        }

        if (alternativeApartment.MaxOccupants.HasValue && (int)alternativeApartment.MaxOccupants < occupantCount)
        {
            throw new InvalidOperationException("Alternative apartment does not satisfy occupancy requirements.");
        }

        if ((booking.NoOfPets ?? 0) > 0 && alternativeApartment.IsPetAllowed != true)
        {
            throw new InvalidOperationException("Alternative apartment does not allow pets for this booking.");
        }

        await EnsureNoConflictingBookingsAsync(alternativeApartmentId, booking.CheckInDate, booking.CheckOutDate);

        var now = DateTime.UtcNow;
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

        var createdOffer = await _bookingOfferRepository.GetOfferWithDetailsAsync(offer.OfferId)
            ?? throw new InvalidOperationException("Offer created but failed to load details.");

        return MapOfferToResponse(createdOffer);
    }

    public async Task<IReadOnlyList<BookingOfferResponseDto>> GetTenantActiveOffersAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var offers = await _bookingOfferRepository.GetPendingOffersForTenantAsync(tenantId, now);
        return offers.Select(MapOfferToResponse).ToList();
    }

    public async Task<BookingOfferResponseDto> RespondToAlternativeOfferAsync(Guid offerId, Guid tenantId, bool accepted, string? notes = null)
    {
        var now = DateTime.UtcNow;
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
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

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
}
