using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using BLL.Mappings;
using BLL.Services.Implements;
using Common.DTOs;
using Common.Settings;
using DAL.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace BLL.Tests;

public class BookingAlternativeSearchTests
{
    [Fact]
    public async Task FindAlternativeApartmentsAsync_MatchesCityAndDistrictIgnoringWhitespaceAndCase()
    {
        var bookingId = Guid.NewGuid();
        var sourceApartmentId = Guid.NewGuid();
        var alternativeApartmentId = Guid.NewGuid();

        var sourceApartment = new Apartment
        {
            ApartmentId = sourceApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Source apartment",
            City = "Ho Chi Minh City ",
            District = "District 1 ",
            BasePricePerNight = 1000000m,
            Status = "posted",
            Location = new Point(106.7000, 10.7769)
        };

        var alternativeApartment = new Apartment
        {
            ApartmentId = alternativeApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Alternative apartment",
            City = "ho chi minh city",
            District = "district 1",
            BasePricePerNight = 1000000m,
            Status = "posted",
            MaxOccupants = 4,
            IsPetAllowed = true,
            Location = new Point(106.7010, 10.7775)
        };

        var booking = new Booking
        {
            BookingId = bookingId,
            ApartmentId = sourceApartmentId,
            TenantId = Guid.NewGuid(),
            CheckInDate = new DateOnly(2026, 5, 20),
            CheckOutDate = new DateOnly(2026, 5, 22),
            Nights = 2,
            NoOfAdults = 2,
            NoOfChildren = 0,
            NoOfInfants = 0,
            NoOfPets = 0,
            TotalPrice = 2000000m,
            Status = "confirmed"
        };

        var sut = CreateSut(sourceApartment, alternativeApartment, booking);

        var (results, totalCount) = await sut.FindAlternativeApartmentsAsync(bookingId);

        Assert.Single(results);
        Assert.Equal(1, totalCount);
        Assert.Equal(alternativeApartmentId, results[0].ApartmentId);
        Assert.Equal("Alternative apartment", results[0].ApartmentTitle);
    }

    [Fact]
    public async Task FindAlternativeApartmentsAsync_FiltersByRadius()
    {
        var bookingId = Guid.NewGuid();
        var sourceApartmentId = Guid.NewGuid();
        var nearApartmentId = Guid.NewGuid();
        var farApartmentId = Guid.NewGuid();

        // Source: coordinates near central point
        var sourceApartment = new Apartment
        {
            ApartmentId = sourceApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Source apartment",
            City = "Hanoi",
            District = "Hoan Kiem",
            BasePricePerNight = 100m,
            Status = "posted",
            Latitude = 21.028511m,
            Longitude = 105.804817m,
            Location = new Point(105.804817, 21.028511)
        };

        // Near: within 0.5 km radius
        var nearApartment = new Apartment
        {
            ApartmentId = nearApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Near apartment",
            City = "Hanoi",
            District = "Hoan Kiem",
            BasePricePerNight = 1000m,
            Status = "posted",
            Latitude = 21.0293m,
            Longitude = 105.8050m,
            MaxOccupants = 2,
            Location = new Point(105.8050, 21.0293)
        };

        // Far: outside the radius but otherwise a valid match
        var farApartment = new Apartment
        {
            ApartmentId = farApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Far apartment",
            City = "Hanoi",
            District = "Hoan Kiem",
            BasePricePerNight = 100m,
            Status = "posted",
            Latitude = 21.0700m,
            Longitude = 105.8800m,
            MaxOccupants = 2,
            Location = new Point(105.8800, 21.0700)
        };

        var booking = new Booking
        {
            BookingId = bookingId,
            ApartmentId = sourceApartmentId,
            TenantId = Guid.NewGuid(),
            CheckInDate = new DateOnly(2026, 5, 20),
            CheckOutDate = new DateOnly(2026, 5, 22),
            Nights = 2,
            NoOfAdults = 1,
            TotalPrice = 200m,
            Status = "confirmed"
        };

        var apartmentRepo = new InMemoryApartmentRepository(sourceApartment, nearApartment, farApartment);
        var sut = CreateSutWithRepos(apartmentRepo, booking);

        // radius 0.5 km should include near but exclude far
        var (results, totalCount) = await sut.FindAlternativeApartmentsAsync(bookingId, page: 1, pageSize: 5, radiusKilometers: 0.5);
        Assert.Single(results);
        Assert.Equal(1, totalCount);
        Assert.Equal(nearApartmentId, results[0].ApartmentId);
        Assert.NotNull(results[0].DistanceKm);
        Assert.InRange(results[0].DistanceKm!.Value, 0d, 0.5d);
    }

    [Fact]
    public async Task EvaluateAlternativeApartmentsAsync_ReturnsEligibilityAndReasons()
    {
        var bookingId = Guid.NewGuid();
        var sourceApartmentId = Guid.NewGuid();
        var eligibleApartmentId = Guid.NewGuid();
        var ineligibleApartmentId = Guid.NewGuid();

        var sourceApartment = new Apartment
        {
            ApartmentId = sourceApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Source apartment",
            City = "Da Nang",
            District = "Hai Chau",
            BasePricePerNight = 100m,
            Status = "posted",
            Latitude = 16.0471m,
            Longitude = 108.2068m,
            Location = new Point(108.2068, 16.0471)
        };

        var eligibleApartment = new Apartment
        {
            ApartmentId = eligibleApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Eligible apartment",
            City = "Da Nang",
            District = "Hai Chau",
            BasePricePerNight = 120m,
            Status = "posted",
            MaxOccupants = 3,
            IsPetAllowed = true,
            Latitude = 16.0478m,
            Longitude = 108.2070m,
            Location = new Point(108.2070, 16.0478)
        };

        var ineligibleApartment = new Apartment
        {
            ApartmentId = ineligibleApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Ineligible apartment",
            City = "Da Nang",
            District = "Hai Chau",
            BasePricePerNight = 130m,
            Status = "posted",
            MaxOccupants = 3,
            IsPetAllowed = true,
            Latitude = 16.1200m,
            Longitude = 108.3000m,
            Location = new Point(108.3000, 16.1200)
        };

        var booking = new Booking
        {
            BookingId = bookingId,
            ApartmentId = sourceApartmentId,
            TenantId = Guid.NewGuid(),
            CheckInDate = new DateOnly(2026, 6, 1),
            CheckOutDate = new DateOnly(2026, 6, 3),
            Nights = 2,
            NoOfAdults = 2,
            NoOfChildren = 0,
            NoOfInfants = 0,
            NoOfPets = 0,
            TotalPrice = 200m,
            Status = "confirmed"
        };

        var apartmentRepo = new InMemoryApartmentRepository(sourceApartment, eligibleApartment, ineligibleApartment);
        var sut = CreateSutWithRepos(apartmentRepo, booking);

        var (assessments, totalCount) = await sut.EvaluateAlternativeApartmentsAsync(bookingId, page: 1, pageSize: 5, radiusKilometers: 5);
        Assert.Equal(2, totalCount);

        var eligible = Assert.Single(assessments.Where(a => a.ApartmentId == eligibleApartmentId));
        Assert.True(eligible.CanBeAlternative);
        Assert.Contains("Meets all occupied alternative requirements.", eligible.Reasons);

        var ineligible = Assert.Single(assessments.Where(a => a.ApartmentId == ineligibleApartmentId));
        Assert.False(ineligible.CanBeAlternative);
        Assert.Contains(ineligible.Reasons, r => r.Contains("Outside requested radius", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindAlternativeApartmentsAsync_AppliesPaging()
    {
        var bookingId = Guid.NewGuid();
        var sourceApartmentId = Guid.NewGuid();
        var alternative1Id = Guid.NewGuid();
        var alternative2Id = Guid.NewGuid();

        var sourceApartment = new Apartment
        {
            ApartmentId = sourceApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Source apartment",
            City = "Can Tho",
            District = "Ninh Kieu",
            BasePricePerNight = 100m,
            Status = "posted",
            Latitude = 10.0340m,
            Longitude = 105.7860m,
            Location = new Point(105.7860, 10.0340)
        };

        var alternative1 = new Apartment
        {
            ApartmentId = alternative1Id,
            LandlordId = Guid.NewGuid(),
            Title = "Alternative 1",
            City = "Can Tho",
            District = "Ninh Kieu",
            BasePricePerNight = 110m,
            Status = "posted",
            MaxOccupants = 2,
            Latitude = 10.0345m,
            Longitude = 105.7865m,
            Location = new Point(105.7865, 10.0345)
        };

        var alternative2 = new Apartment
        {
            ApartmentId = alternative2Id,
            LandlordId = Guid.NewGuid(),
            Title = "Alternative 2",
            City = "Can Tho",
            District = "Ninh Kieu",
            BasePricePerNight = 120m,
            Status = "posted",
            MaxOccupants = 2,
            Latitude = 10.0350m,
            Longitude = 105.7870m,
            Location = new Point(105.7870, 10.0350)
        };

        var booking = new Booking
        {
            BookingId = bookingId,
            ApartmentId = sourceApartmentId,
            TenantId = Guid.NewGuid(),
            CheckInDate = new DateOnly(2026, 6, 10),
            CheckOutDate = new DateOnly(2026, 6, 12),
            Nights = 2,
            NoOfAdults = 1,
            NoOfChildren = 0,
            NoOfInfants = 0,
            NoOfPets = 0,
            TotalPrice = 200m,
            Status = "confirmed"
        };

        var apartmentRepo = new InMemoryApartmentRepository(sourceApartment, alternative1, alternative2);
        var sut = CreateSutWithRepos(apartmentRepo, booking);

        var (page1Items, totalCount) = await sut.FindAlternativeApartmentsAsync(bookingId, page: 1, pageSize: 1);
        var (page2Items, _) = await sut.FindAlternativeApartmentsAsync(bookingId, page: 2, pageSize: 1);

        Assert.Equal(2, totalCount);
        Assert.Single(page1Items);
        Assert.Single(page2Items);
        Assert.NotEqual(page1Items[0].ApartmentId, page2Items[0].ApartmentId);
    }

    private static BookingService CreateSutWithRepos(InMemoryApartmentRepository apartmentRepo, Booking booking)
    {
        var bookingRepo = new InMemoryBookingRepository(new[] { booking });
        var bookingOfferRepo = new InMemoryBookingOfferRepository();
        var calendarRepo = new InMemoryApartmentPriceCalendarRepository();
        var packageRepo = new InMemoryRepository<Package>(p => p.PackageId);
        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var bookingCheckTimeRepo = new InMemoryRepository<BookingCheckTime>(c => c.CheckTimeId);
        var temporaryResidenceRepo = new InMemoryRepository<TemporaryResidenceReport>(r => r.ReportId);
        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId);
        var userRepo = new InMemoryRepository<User>(u => u.UserId);
        var availabilityRepo = new InMemoryRepository<ApartmentAvailability>(a => a.AvailabilityId);
        var supportTicketRepo = new FakeSupportTicketRepository(new InMemoryRepository<SupportTicket>(s => s.TicketId));
        var paymentRepo = new InMemoryRepository<Payment>(p => p.PaymentId);
        var checkTimeStateEventRepo = new InMemoryRepository<BookingCheckTimeStateEvent>(e => e.EventId);
        var configuration = new ConfigurationManager();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ApartmentProfile>(), NullLoggerFactory.Instance).CreateMapper();

        // New dependencies
        var payOsClient = new FakePayOSClient();
        var stripeSettings = Options.Create(new StripeSettings());
        var payOsPayoutService = new FakePayOSPayoutService();
        var landlordWalletService = new RecordingWalletService(); // implements ILandlordWalletService

        return new BookingService(
            bookingRepo,                // 1  IBookingRepository
            bookingOfferRepo,           // 2  IBookingOfferRepository
            apartmentRepo,              // 3  IApartmentRepository
            calendarRepo,               // 4  IApartmentPriceCalendarRepository
            packageRepo,                // 5  IRepository<Package>
            notificationRepo,           // 6  IRepository<Notification>
            bookingCheckTimeRepo,       // 7  IRepository<BookingCheckTime>
            temporaryResidenceRepo,     // 8  IRepository<TemporaryResidenceReport>
            tenantRepo,                 // 9  IRepository<Tenant>
            userRepo,                   // 10 IRepository<User>
            availabilityRepo,           // 11 IRepository<ApartmentAvailability>
            supportTicketRepo,          // 12 IRepository<SupportTicket>
            paymentRepo,                // 13 IRepository<Payment>
            new StripeServiceStub(),    // 14 IStripeService
            payOsClient,                // 15 PayOSClient
            new FakeMomoService(),      // 16 IMomoService
            stripeSettings,             // 17 IOptions<StripeSettings>
            payOsPayoutService,         // 18 IPayOSPayoutService
            new NoOpIdentityVerificationService(), // 19 IIdentityVerificationService
            landlordWalletService,      // 20 ILandlordWalletService
            configuration,              // 21 IConfiguration
            Options.Create(new Common.Settings.BookingAdmissionPolicySettings()),
            mapper,                     // 22 IMapper
            null,                       // 23 IRepository<BookingOccupant>?
            null,                       // 24 ICheckTimeRequestRepository?
            checkTimeStateEventRepo     // 25 IRepository<BookingCheckTimeStateEvent>?
        );
    }

    private static BookingService CreateSut(Apartment sourceApartment, Apartment alternativeApartment, Booking booking)
    {
        var apartmentRepo = new InMemoryApartmentRepository(sourceApartment, alternativeApartment);
        return CreateSutWithRepos(apartmentRepo, booking);
    }
}