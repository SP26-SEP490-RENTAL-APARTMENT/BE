using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;

namespace BLL.Services.Implements;

public class BookingService : BaseService<Booking>, IBookingService
{
    private const decimal SuggestedDepositRate = 0.30m;

    private readonly IBookingRepository _bookingRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IApartmentPriceCalendarRepository _apartmentPriceCalendarRepository;
    private readonly IRepository<Package> _packageRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<BookingCheckTime> _bookingCheckTimeRepository;
    private readonly IRepository<TemporaryResidenceReport> _temporaryResidenceReportRepository;
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IIdentityVerificationService _identityVerificationService;

    public BookingService(
        IBookingRepository repository,
        IApartmentRepository apartmentRepository,
        IApartmentPriceCalendarRepository apartmentPriceCalendarRepository,
        IRepository<Package> packageRepository,
        IRepository<Notification> notificationRepository,
        IRepository<BookingCheckTime> bookingCheckTimeRepository,
        IRepository<TemporaryResidenceReport> temporaryResidenceReportRepository,
        IRepository<Tenant> tenantRepository,
        IRepository<User> userRepository,
        IIdentityVerificationService identityVerificationService) : base(repository)
    {
        _bookingRepository = repository;
        _apartmentRepository = apartmentRepository;
        _apartmentPriceCalendarRepository = apartmentPriceCalendarRepository;
        _packageRepository = packageRepository;
        _notificationRepository = notificationRepository;
        _bookingCheckTimeRepository = bookingCheckTimeRepository;
        _temporaryResidenceReportRepository = temporaryResidenceReportRepository;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _identityVerificationService = identityVerificationService;
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
            CheckInDate = requestDto.CheckInDate,
            CheckOutDate = requestDto.CheckOutDate
        });

        var depositAmount = requestDto.DepositAmount > 0 ? requestDto.DepositAmount : quote.SuggestedDeposit;
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
            BalanceDueDate = requestDto.BalanceDueDate,
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

        return booking;
    }

    public async Task<Booking> MarkDepositPaidAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        await _identityVerificationService.EnsureUserVerifiedForBookingAsync(booking.TenantId);

        booking.DepositPaid = true;
        if (string.Equals(booking.Status, "pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "negotiating", StringComparison.OrdinalIgnoreCase))
        {
            booking.Status = "confirmed";
        }

        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment != null)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = apartment.LandlordId,
                Type = "booking_confirmed",
                Title = "New booking confirmed",
                Message = $"A booking for apartment '{apartment.Title}' has been confirmed with deposit payment.",
                ReferenceId = booking.BookingId,
                ReferenceType = "booking",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _notificationRepository.SaveChangesAsync();
        }

        return booking;
    }

    public async Task<Booking> MarkBalancePaidAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new ArgumentException("Booking not found.");

        if (booking.DepositPaid != true)
            throw new InvalidOperationException("Deposit must be paid before settling remaining balance.");

        booking.Status = "paid";
        _bookingRepository.Update(booking);
        await _bookingRepository.SaveChangesAsync();

        return booking;
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
    }
}
