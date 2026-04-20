using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;

namespace BLL.Tests;

public class LandlordPayoutServiceTests
{
    [Fact]
    public async Task CreatePayoutAsync_Success_FinalizesWalletAndCreatesTransaction()
    {
        var landlordId = Guid.NewGuid();
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId, new Landlord
        {
            LandlordId = landlordId,
            PayoutReceiverName = "Landlord A",
            MomoWalletPhone = "0900000001"
        });

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId);
        var momoService = new FakeMomoService
        {
            CreateDisbursementResult = new MomoDisbursementResponse
            {
                ResultCode = 0,
                Message = "Success",
                OrderId = "ORD-1",
                RequestId = "REQ-1",
                TransId = "TRX-1",
                Amount = 1500,
                RequestRaw = "{}",
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);
        var request = new CreateLandlordPayoutRequestDto { Amount = 1500, Channel = "wallet", OrderInfo = "payout" };

        var result = await sut.CreatePayoutAsync(landlordId, request, CancellationToken.None);

        Assert.Equal("success", result.Status);
        Assert.Equal(1500, result.Amount);
        Assert.Single(walletService.ReservedAmounts);
        Assert.Single(walletService.FinalizedAmounts);
        Assert.Empty(walletService.RolledBackAmounts);
        Assert.Single(payoutRepo.Items);
        Assert.Single(momoTransactionService.Items);
        Assert.Equal("success", momoTransactionService.Items[0].Status);
    }

    [Fact]
    public async Task CreatePayoutAsync_Failed_RollsBackWallet()
    {
        var landlordId = Guid.NewGuid();
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId, new Landlord
        {
            LandlordId = landlordId,
            PayoutReceiverName = "Landlord B",
            MomoWalletPhone = "0900000002"
        });

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId);
        var momoService = new FakeMomoService
        {
            CreateDisbursementResult = new MomoDisbursementResponse
            {
                ResultCode = 42,
                Message = "Failure",
                OrderId = "ORD-2",
                RequestId = "REQ-2",
                Amount = 1500,
                RequestRaw = "{}",
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);
        var request = new CreateLandlordPayoutRequestDto { Amount = 1500, Channel = "wallet", OrderInfo = "payout" };

        var result = await sut.CreatePayoutAsync(landlordId, request, CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Single(walletService.ReservedAmounts);
        Assert.Empty(walletService.FinalizedAmounts);
        Assert.Single(walletService.RolledBackAmounts);
        Assert.Single(payoutRepo.Items);
        Assert.Single(momoTransactionService.Items);
        Assert.Equal("failed", momoTransactionService.Items[0].Status);
    }

    [Fact]
    public async Task SyncProcessingPayoutsAsync_TransitionsToSuccess_FinalizesWallet()
    {
        var landlordId = Guid.NewGuid();
        var payout = new LandlordPayout
        {
            PayoutId = Guid.NewGuid(),
            LandlordId = landlordId,
            Amount = 2000,
            FeeAmount = 0,
            NetAmount = 2000,
            Channel = "wallet",
            Status = "processing",
            MomoOrderId = "ORD-3",
            MomoRequestId = "REQ-3",
            MomoTransId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId, payout);
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);
        var momoService = new FakeMomoService
        {
            QueryDisbursementResult = new MomoQueryDisbursementResponse
            {
                ResultCode = 0,
                Message = "Completed",
                OrderId = "ORD-3",
                RequestId = "REQ-3",
                TransId = "TRX-3",
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);

        var updated = await sut.SyncProcessingPayoutsAsync(CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Equal("success", payout.Status);
        Assert.NotNull(payout.CompletedAt);
        Assert.Single(walletService.FinalizedAmounts);
        Assert.Empty(walletService.RolledBackAmounts);
    }

    [Fact]
    public async Task SyncProcessingPayoutsAsync_TransitionsToFailed_RollsBackWallet()
    {
        var landlordId = Guid.NewGuid();
        var payout = new LandlordPayout
        {
            PayoutId = Guid.NewGuid(),
            LandlordId = landlordId,
            Amount = 3000,
            FeeAmount = 0,
            NetAmount = 3000,
            Channel = "wallet",
            Status = "requested",
            MomoOrderId = "ORD-4",
            MomoRequestId = "REQ-4",
            MomoTransId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId, payout);
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);
        var momoService = new FakeMomoService
        {
            QueryDisbursementResult = new MomoQueryDisbursementResponse
            {
                ResultCode = 99,
                Message = "Rejected",
                OrderId = "ORD-4",
                RequestId = "REQ-4",
                TransId = null,
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);

        var updated = await sut.SyncProcessingPayoutsAsync(CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Equal("failed", payout.Status);
        Assert.NotNull(payout.FailedAt);
        Assert.Empty(walletService.FinalizedAmounts);
        Assert.Single(walletService.RolledBackAmounts);
    }
}

public class LandlordWalletServiceTests
{
    [Fact]
    public async Task ApplyOccupiedIncidentPenaltyAsync_UsesAvailableThenPending_WhenFundsAreEnough()
    {
        var landlordId = Guid.NewGuid();
        var walletRepo = new InMemoryRepository<LandlordWallet>(
            w => w.LandlordId,
            new LandlordWallet
            {
                LandlordId = landlordId,
                AvailableBalance = 100m,
                PendingBalance = 50m,
                UpdatedAt = DateTime.UtcNow
            }
        );

        var sut = new LandlordWalletService(walletRepo);

        var result = await sut.ApplyOccupiedIncidentPenaltyAsync(landlordId, 120m);

        var wallet = walletRepo.Items.Single();
        Assert.Equal(0m, wallet.AvailableBalance);
        Assert.Equal(30m, wallet.PendingBalance);
        Assert.Equal(100m, result.DeductedFromAvailable);
        Assert.Equal(20m, result.DeductedFromPending);
        Assert.Equal(0m, result.DebtRecorded);
    }

    [Fact]
    public async Task ApplyOccupiedIncidentPenaltyAsync_RecordsDebt_WhenFundsAreInsufficient()
    {
        var landlordId = Guid.NewGuid();
        var walletRepo = new InMemoryRepository<LandlordWallet>(
            w => w.LandlordId,
            new LandlordWallet
            {
                LandlordId = landlordId,
                AvailableBalance = 10m,
                PendingBalance = 5m,
                UpdatedAt = DateTime.UtcNow
            }
        );

        var sut = new LandlordWalletService(walletRepo);

        var result = await sut.ApplyOccupiedIncidentPenaltyAsync(landlordId, 40m);

        var wallet = walletRepo.Items.Single();
        Assert.Equal(-25m, wallet.AvailableBalance);
        Assert.Equal(0m, wallet.PendingBalance);
        Assert.Equal(10m, result.DeductedFromAvailable);
        Assert.Equal(5m, result.DeductedFromPending);
        Assert.Equal(25m, result.DebtRecorded);
    }
}

public class BookingServiceQuoteValidationTests
{
    [Fact]
    public async Task GetQuoteAsync_ThrowsWhenCheckoutIsNotLaterThanCheckin()
    {
        var sut = FinancialTestHelpers.CreateBookingService();

        var dto = new BookingQuoteRequestDto
        {
            ApartmentId = Guid.NewGuid(),
            CheckInDate = new DateOnly(2026, 5, 10),
            CheckOutDate = new DateOnly(2026, 5, 10),
            NoOfAdults = 1,
            NoOfInfants = 0,
            NoOfPets = 0
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.GetQuoteAsync(dto));

        Assert.Contains("Check-out date must be later than check-in date", ex.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_ThrowsWhenApartmentIsMissing()
    {
        var sut = FinancialTestHelpers.CreateBookingService();

        var dto = FinancialTestHelpers.CreateValidQuoteRequest(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.GetQuoteAsync(dto));

        Assert.Contains("Apartment not found", ex.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_ThrowsWhenApartmentIsNotPosted()
    {
        var apartmentId = Guid.NewGuid();
        var sut = FinancialTestHelpers.CreateBookingService(
            apartment: FinancialTestHelpers.CreateApartment(apartmentId, status: "draft"));

        var dto = FinancialTestHelpers.CreateValidQuoteRequest(apartmentId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetQuoteAsync(dto));

        Assert.Contains("not currently available for booking", ex.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_ThrowsWhenApartmentIsLocked()
    {
        var apartmentId = Guid.NewGuid();
        var sut = FinancialTestHelpers.CreateBookingService(
            apartment: FinancialTestHelpers.CreateApartment(apartmentId, bookingStatus: "Locked"));

        var dto = FinancialTestHelpers.CreateValidQuoteRequest(apartmentId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetQuoteAsync(dto));

        Assert.Contains("locked for booking", ex.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_ThrowsWhenMinimumStayIsNotMet()
    {
        var apartmentId = Guid.NewGuid();
        var calendar = new ApartmentPriceCalendar
        {
            PriceId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 31),
            MinNights = 3
        };

        var sut = FinancialTestHelpers.CreateBookingService(
            apartment: FinancialTestHelpers.CreateApartment(apartmentId),
            calendars: [calendar]);

        var dto = new BookingQuoteRequestDto
        {
            ApartmentId = apartmentId,
            CheckInDate = new DateOnly(2026, 5, 10),
            CheckOutDate = new DateOnly(2026, 5, 12),
            NoOfAdults = 1,
            NoOfInfants = 0,
            NoOfPets = 0
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetQuoteAsync(dto));

        Assert.Contains("at least 3 night(s)", ex.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_ThrowsWhenPackageDoesNotBelongToApartment()
    {
        var apartmentId = Guid.NewGuid();
        var package = new Package
        {
            PackageId = Guid.NewGuid(),
            ApartmentId = Guid.NewGuid(),
            Name = "Wrong package",
            Price = 500000m,
            IsActive = true
        };

        var sut = FinancialTestHelpers.CreateBookingService(
            apartment: FinancialTestHelpers.CreateApartment(apartmentId),
            packages: [package]);

        var dto = FinancialTestHelpers.CreateValidQuoteRequest(apartmentId, package.PackageId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.GetQuoteAsync(dto));

        Assert.Contains("Selected package is invalid for this apartment", ex.Message);
    }
}

public class BookingServiceResidenceReportTests
{
    [Fact]
    public async Task GetResidenceReportDetailsAsync_ReturnsDetailsForConfirmedBooking()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();

        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = landlordId,
            Title = "Residence Apartment",
            Address = "123 Main Street",
            District = "District 1",
            City = "Hanoi",
            Status = "posted"
        };

        var bookingRepo = new InMemoryBookingRepository(new[]
        {
            new Booking
            {
                BookingId = bookingId,
                TenantId = tenantId,
                ApartmentId = apartmentId,
                CheckInDate = new DateOnly(2026, 5, 1),
                CheckOutDate = new DateOnly(2026, 5, 5),
                Nights = 4,
                TotalPrice = 4000000m,
                DepositAmount = 1200000m,
                UpfrontPaymentAmount = 1200000m,
                BalanceDueDate = new DateOnly(2026, 4, 25),
                Status = "confirmed"
            }
        });

        var apartmentRepo = new InMemoryApartmentRepository(apartment);
        var reportRepo = new InMemoryRepository<TemporaryResidenceReport>(r => r.ReportId,
            new TemporaryResidenceReport
            {
                ReportId = Guid.NewGuid(),
                BookingId = bookingId,
                LandlordId = landlordId,
                TenantPassportId = "P1234567",
                TenantNationality = "VN",
                ReportedToPolice = true,
                ReportDate = new DateOnly(2026, 5, 6),
                ReportNumber = "RPT-001"
            });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId);
        var userRepo = new InMemoryRepository<User>(u => u.UserId,
            new User { UserId = landlordId, FullName = "Landlord User", Phone = "0900000001", Role = "landlord" },
            new User { UserId = tenantId, FullName = "Tenant User", Phone = "0900000002", Role = "tenant" });

        var sut = new BookingService(
            bookingRepo,
            new InMemoryBookingOfferRepository(),
            apartmentRepo,
            new InMemoryApartmentPriceCalendarRepository(),
            new InMemoryRepository<Package>(p => p.PackageId),
            new InMemoryRepository<DAL.Models.Notification>(n => n.NotificationId),
            new InMemoryRepository<BookingCheckTime>(c => c.CheckTimeId),
            reportRepo,
            tenantRepo,
            userRepo,
            new InMemoryRepository<ApartmentAvailability>(a => a.AvailabilityId),
            new InMemoryRepository<SupportTicket>(s => s.TicketId),
            new InMemoryRepository<Payment>(p => p.PaymentId),
            new NoOpIdentityVerificationService(),
            new RecordingWalletService(),
            new ConfigurationManager(),
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper());

        var details = await sut.GetResidenceReportDetailsAsync(bookingId, landlordId);

        Assert.Equal(bookingId, details.BookingId);
        Assert.Equal(landlordId, details.LandlordId);
        Assert.Equal(tenantId, details.TenantId);
        Assert.Equal("Residence Apartment", details.ApartmentTitle);
        Assert.Equal("RPT-001", details.ReportNumber);
    }

    [Fact]
    public async Task GetResidenceReportDetailsAsync_ThrowsWhenBookingIsNotConfirmed()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();

        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = landlordId,
            Title = "Residence Apartment",
            Status = "posted"
        };

        var bookingRepo = new InMemoryBookingRepository(new[]
        {
            new Booking
            {
                BookingId = bookingId,
                TenantId = tenantId,
                ApartmentId = apartmentId,
                CheckInDate = new DateOnly(2026, 5, 1),
                CheckOutDate = new DateOnly(2026, 5, 5),
                Nights = 4,
                TotalPrice = 4000000m,
                DepositAmount = 1200000m,
                UpfrontPaymentAmount = 1200000m,
                BalanceDueDate = new DateOnly(2026, 4, 25),
                Status = "pending"
            }
        });

        var sut = new BookingService(
            bookingRepo,
            new InMemoryBookingOfferRepository(),
            new InMemoryApartmentRepository(apartment),
            new InMemoryApartmentPriceCalendarRepository(),
            new InMemoryRepository<Package>(p => p.PackageId),
            new InMemoryRepository<DAL.Models.Notification>(n => n.NotificationId),
            new InMemoryRepository<BookingCheckTime>(c => c.CheckTimeId),
            new InMemoryRepository<TemporaryResidenceReport>(r => r.ReportId),
            new InMemoryRepository<Tenant>(t => t.TenantId),
            new InMemoryRepository<User>(u => u.UserId),
            new InMemoryRepository<ApartmentAvailability>(a => a.AvailabilityId),
            new InMemoryRepository<SupportTicket>(s => s.TicketId),
            new InMemoryRepository<Payment>(p => p.PaymentId),
            new NoOpIdentityVerificationService(),
            new RecordingWalletService(),
            new ConfigurationManager(),
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetResidenceReportDetailsAsync(bookingId, landlordId));

        Assert.Contains("confirmed status", ex.Message);
    }
}

public class LandlordPayoutValidationTests
{
    [Fact]
    public async Task CreatePayoutAsync_ThrowsWhenAmountIsZero()
    {
        var landlordId = Guid.NewGuid();
        var sut = FinancialTestHelpers.CreatePayoutService(
            landlord: new Landlord
            {
                LandlordId = landlordId,
                PayoutReceiverName = "Landlord",
                MomoWalletPhone = "0900000001"
            });

        var request = new CreateLandlordPayoutRequestDto
        {
            Amount = 0,
            Channel = "wallet"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.CreatePayoutAsync(landlordId, request, CancellationToken.None));

        Assert.Contains("Amount must be greater than zero", ex.Message);
    }

    [Fact]
    public async Task CreatePayoutAsync_ThrowsWhenLandlordProfileIsMissing()
    {
        var sut = FinancialTestHelpers.CreatePayoutService();

        var request = new CreateLandlordPayoutRequestDto
        {
            Amount = 1500,
            Channel = "wallet"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.CreatePayoutAsync(Guid.NewGuid(), request, CancellationToken.None));

        Assert.Contains("Landlord profile not found", ex.Message);
    }

    [Fact]
    public async Task CreatePayoutAsync_ThrowsWhenBankProfileIsIncomplete()
    {
        var landlordId = Guid.NewGuid();
        var sut = FinancialTestHelpers.CreatePayoutService(
            landlord: new Landlord
            {
                LandlordId = landlordId,
                PayoutReceiverName = "Landlord",
                PayoutBankAccountNo = null,
                PayoutBankCardNo = null,
                PayoutBankCode = null
            });

        var request = new CreateLandlordPayoutRequestDto
        {
            Amount = 10000,
            Channel = "bank"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreatePayoutAsync(landlordId, request, CancellationToken.None));

        Assert.Contains("Bank payout profile is incomplete", ex.Message);
    }
}

internal static class FinancialTestHelpers
{
    public static BookingService CreateBookingService(
        Apartment? apartment = null,
        IEnumerable<Booking>? bookings = null,
        IEnumerable<ApartmentAvailability>? availabilities = null,
        IEnumerable<ApartmentPriceCalendar>? calendars = null,
        IEnumerable<Package>? packages = null)
    {
        var apartmentRepo = apartment == null
            ? new InMemoryApartmentRepository()
            : new InMemoryApartmentRepository(apartment);

        var bookingRepo = new InMemoryBookingRepository(bookings ?? Array.Empty<Booking>());
        var bookingOfferRepo = new InMemoryBookingOfferRepository();
        var calendarRepo = new InMemoryApartmentPriceCalendarRepository(calendars?.ToArray() ?? Array.Empty<ApartmentPriceCalendar>());
        var packageRepo = new InMemoryRepository<Package>(p => p.PackageId, packages?.ToArray() ?? Array.Empty<Package>());
        var notificationRepo = new InMemoryRepository<DAL.Models.Notification>(n => n.NotificationId);
        var bookingCheckTimeRepo = new InMemoryRepository<BookingCheckTime>(c => c.CheckTimeId);
        var temporaryResidenceRepo = new InMemoryRepository<TemporaryResidenceReport>(r => r.ReportId);
        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId);
        var userRepo = new InMemoryRepository<User>(u => u.UserId);
        var availabilityRepo = new InMemoryRepository<ApartmentAvailability>(a => a.AvailabilityId, availabilities?.ToArray() ?? Array.Empty<ApartmentAvailability>());
        var supportTicketRepo = new InMemoryRepository<SupportTicket>(s => s.TicketId);
        var paymentRepo = new InMemoryRepository<Payment>(p => p.PaymentId);
        var identityVerificationService = new NoOpIdentityVerificationService();
        var walletService = new RecordingWalletService();
        var configuration = new ConfigurationManager();
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();

        return new BookingService(
            bookingRepo,
            bookingOfferRepo,
            apartmentRepo,
            calendarRepo,
            packageRepo,
            notificationRepo,
            bookingCheckTimeRepo,
            temporaryResidenceRepo,
            tenantRepo,
            userRepo,
            availabilityRepo,
            supportTicketRepo,
            paymentRepo,
            identityVerificationService,
            walletService,
            configuration,
            mapper);
    }

    public static LandlordPayoutService CreatePayoutService(Landlord? landlord = null)
    {
        var landlordRepo = landlord == null
            ? new InMemoryRepository<Landlord>(l => l.LandlordId)
            : new InMemoryRepository<Landlord>(l => l.LandlordId, landlord);

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId);
        var momoService = new FakeMomoService();
        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        return new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);
    }

    public static Apartment CreateApartment(Guid apartmentId, string status = "posted", string? bookingStatus = null)
    {
        return new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Test Apartment",
            City = "Hanoi",
            BasePricePerNight = 1000000m,
            Status = status,
            BookingStatus = bookingStatus,
            Location = new Point(0, 0)
        };
    }

    public static BookingQuoteRequestDto CreateValidQuoteRequest(Guid apartmentId, Guid? packageId = null)
    {
        return new BookingQuoteRequestDto
        {
            ApartmentId = apartmentId,
            PackageId = packageId,
            CheckInDate = new DateOnly(2026, 5, 10),
            CheckOutDate = new DateOnly(2026, 5, 12),
            NoOfAdults = 1,
            NoOfInfants = 0,
            NoOfPets = 0
        };
    }
}

internal sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly List<Booking> _items;

    public InMemoryBookingRepository(IEnumerable<Booking>? seed = null)
    {
        _items = seed?.ToList() ?? new List<Booking>();
    }

    public Task AddAsync(Booking entity)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Booking>> FindAsync(Expression<Func<Booking, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult<IEnumerable<Booking>>(_items.Where(compiled));
    }

    public Task<(IEnumerable<Booking> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items: _items.AsEnumerable(), TotalCount: _items.Count));
    }

    public Task<Booking?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_items.FirstOrDefault(b => b.BookingId == id));
    }

    public void Remove(Booking entity)
    {
        _items.Remove(entity);
    }

    public void Update(Booking entity)
    {
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }

    public Task<(IEnumerable<Booking> Items, int TotalCount)> GetByLandlordAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null, IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items: _items.AsEnumerable(), TotalCount: _items.Count));
    }
}

internal sealed class InMemoryBookingOfferRepository : IBookingOfferRepository
{
    private readonly List<BookingOffer> _items = new();

    public Task AddAsync(BookingOffer entity)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<BookingOffer>> FindAsync(Expression<Func<BookingOffer, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult<IEnumerable<BookingOffer>>(_items.Where(compiled));
    }

    public Task<(IEnumerable<BookingOffer> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items: _items.AsEnumerable(), TotalCount: _items.Count));
    }

    public Task<BookingOffer?> GetByIdAsync(Guid id)
    {
        return Task.FromResult<BookingOffer?>(_items.FirstOrDefault(x => x.OfferId == id));
    }

    public void Remove(BookingOffer entity)
    {
        _items.Remove(entity);
    }

    public void Update(BookingOffer entity)
    {
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }

    public Task<BookingOffer?> GetOfferWithDetailsAsync(Guid offerId)
    {
        return GetByIdAsync(offerId);
    }

    public Task<IEnumerable<BookingOffer>> GetPendingOffersForTenantAsync(Guid tenantId, DateTime nowUtc)
    {
        return Task.FromResult<IEnumerable<BookingOffer>>(Array.Empty<BookingOffer>());
    }

    public Task<IEnumerable<BookingOffer>> GetPendingOffersByBookingAsync(Guid bookingId, DateTime nowUtc)
    {
        return Task.FromResult<IEnumerable<BookingOffer>>(Array.Empty<BookingOffer>());
    }
}

internal sealed class InMemoryApartmentPriceCalendarRepository : IApartmentPriceCalendarRepository
{
    private readonly List<ApartmentPriceCalendar> _items;

    public InMemoryApartmentPriceCalendarRepository(IEnumerable<ApartmentPriceCalendar>? seed = null)
    {
        _items = seed?.ToList() ?? new List<ApartmentPriceCalendar>();
    }

    public Task AddAsync(ApartmentPriceCalendar entity)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ApartmentPriceCalendar>> FindAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(_items.Where(compiled));
    }

    public Task<(IEnumerable<ApartmentPriceCalendar> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items: _items.AsEnumerable(), TotalCount: _items.Count));
    }

    public Task<ApartmentPriceCalendar?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_items.FirstOrDefault(x => x.PriceId == id));
    }

    public void Remove(ApartmentPriceCalendar entity)
    {
        _items.Remove(entity);
    }

    public void Update(ApartmentPriceCalendar entity)
    {
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

internal sealed class NoOpIdentityVerificationService : IIdentityVerificationService
{
    public Task EnsureUserVerifiedForBookingAsync(Guid userId)
    {
        return Task.CompletedTask;
    }

    public Task EnsureUserVerifiedForInspectionAsync(Guid landlordId)
    {
        return Task.CompletedTask;
    }

    public Task<Guid[]> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto)
    {
        return Task.FromResult(Array.Empty<Guid>());
    }

    public Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto)
    {
        return Task.CompletedTask;
    }

    public Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetUserDocumentsAsync(Guid userId, int page, int pageSize, string? sortBy = null, string? sortOrder = null)
    {
        return Task.FromResult((Items: Enumerable.Empty<IdentityDocumentDto>(), TotalCount: 0));
    }

    public Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetAllDocumentsAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null)
    {
        return Task.FromResult((Items: Enumerable.Empty<IdentityDocumentDto>(), TotalCount: 0));
    }
}

internal sealed class InMemoryRepository<T> : IRepository<T>
    where T : class
{
    private readonly Func<T, Guid>? _idSelector;

    public InMemoryRepository(Func<T, Guid>? idSelector = null, params T[] seed)
    {
        _idSelector = idSelector;
        Items = seed.ToList();
    }

    public List<T> Items { get; }

    public Task AddAsync(T entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<T> result = Items.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items.AsEnumerable(), Items.Count));
    }

    public Task<T?> GetByIdAsync(Guid id)
    {
        if (_idSelector == null)
        {
            return Task.FromResult<T?>(null);
        }

        var entity = Items.FirstOrDefault(x => _idSelector(x) == id);
        return Task.FromResult(entity);
    }

    public void Remove(T entity)
    {
        Items.Remove(entity);
    }

    public void Update(T entity)
    {
        // No-op for in-memory reference updates.
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

internal sealed class FakeMomoService : IMomoService
{
    public MomoDisbursementResponse CreateDisbursementResult { get; set; } = new();

    public MomoQueryDisbursementResponse QueryDisbursementResult { get; set; } = new();

    public Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MomoDisbursementResponse> VerifyWalletAsync(MomoVerifyWalletRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MomoDisbursementResponse> CreateDisbursementAsync(MomoDisbursementRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDisbursementResult);
    }

    public Task<MomoQueryDisbursementResponse> QueryDisbursementStatusAsync(MomoQueryDisbursementRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(QueryDisbursementResult);
    }

    public Task<MomoQueryPaymentResponse> QueryPaymentStatusAsync(MomoQueryPaymentRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MomoQueryPaymentResponse());
    }

    public bool ValidateDisbursementIpnSignature(string requestBody)
    {
        throw new NotImplementedException();
    }
}

internal sealed class RecordingWalletService : ILandlordWalletService
{
    public List<long> ReservedAmounts { get; } = new();

    public List<long> FinalizedAmounts { get; } = new();

    public List<long> RolledBackAmounts { get; } = new();

    public Task<LandlordWallet> GetOrCreateAsync(Guid landlordId)
    {
        return Task.FromResult(new LandlordWallet { LandlordId = landlordId });
    }

    public Task CreditPendingAsync(Guid landlordId, decimal amount)
    {
        return Task.CompletedTask;
    }

    public Task DebitAvailableAsync(Guid landlordId, decimal amount)
    {
        return Task.CompletedTask;
    }

    public Task<LandlordPenaltyApplicationResultDto> ApplyOccupiedIncidentPenaltyAsync(Guid landlordId, decimal amount)
    {
        throw new NotImplementedException();
    }

    public Task ReserveForPayoutAsync(Guid landlordId, long amount)
    {
        ReservedAmounts.Add(amount);
        return Task.CompletedTask;
    }

    public Task FinalizePayoutSuccessAsync(Guid landlordId, long amount)
    {
        FinalizedAmounts.Add(amount);
        return Task.CompletedTask;
    }

    public Task RollbackPayoutAsync(Guid landlordId, long amount)
    {
        RolledBackAmounts.Add(amount);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMomoTransactionService : IMomoTransactionService
{
    public List<MomoTransaction> Items { get; } = new();

    public Task<MomoTransaction?> GetByIdAsync(Guid id)
    {
        return Task.FromResult<MomoTransaction?>(null);
    }

    public Task<(IEnumerable<MomoTransaction> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items.AsEnumerable(), Items.Count));
    }

    public Task<MomoTransaction> CreateAsync(MomoTransaction entity)
    {
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(MomoTransaction entity)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        return Task.CompletedTask;
    }

    public Task<MomoTransaction?> FindByRequestIdAsync(string requestId)
    {
        var result = Items.FirstOrDefault(i => i.RequestId == requestId);
        return Task.FromResult(result);
    }

    public Task<MomoTransaction?> FindByRequestBodyContainsAsync(string content)
    {
        var result = Items.FirstOrDefault(i => i.RequestBody.Contains(content, StringComparison.Ordinal));
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<MomoTransaction>> GetPendingIpnQueueItemsAsync(int take, DateTime retryReadyAtOrBefore)
    {
        var items = Items
            .Where(i => i.Type == "ipn_queue"
                && (i.Status == "queued" || (i.Status == "retry_wait" && i.UpdatedAt <= retryReadyAtOrBefore)))
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<MomoTransaction>>(items);
    }

    public Task<IReadOnlyList<MomoTransaction>> GetPendingWalletPaymentRequestsAsync(int take, DateTime createdBefore)
    {
        var items = Items
            .Where(i => (i.Type == "create_wallet_payment" || i.Type == "create_wallet_payment_subscription")
                && i.Status == "pending"
                && i.CreatedAt <= createdBefore)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<MomoTransaction>>(items);
    }
}
