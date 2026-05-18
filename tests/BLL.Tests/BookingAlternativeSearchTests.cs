using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using BLL.Mappings;
using BLL.Services.Implements;
using Common.DTOs;
using DAL.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

        var results = await sut.FindAlternativeApartmentsAsync(bookingId);

        Assert.Single(results);
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

        // Near: ~100m away
        var nearApartment = new Apartment
        {
            ApartmentId = nearApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Near apartment",
            City = "Hanoi",
            District = "Hoan Kiem",
            BasePricePerNight = 100m,
            Status = "posted",
            Latitude = 21.0293m,
            Longitude = 105.8050m,
            MaxOccupants = 2,
            Location = new Point(105.8050, 21.0293)
        };

        // Far: ~5km away
        var farApartment = new Apartment
        {
            ApartmentId = farApartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Far apartment",
            City = "Hanoi",
            District = "Long Bien",
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

        // radius 500 meters should include near but exclude far
        var results = await sut.FindAlternativeApartmentsAsync(bookingId, maxResults: 5, radiusMeters: 500);
        Assert.Single(results);
        Assert.Equal(nearApartmentId, results[0].ApartmentId);
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
        var supportTicketRepo = new InMemoryRepository<SupportTicket>(s => s.TicketId);
        var paymentRepo = new InMemoryRepository<Payment>(p => p.PaymentId);
        var checkTimeStateEventRepo = new InMemoryRepository<BookingCheckTimeStateEvent>(e => e.EventId);
        var configuration = new ConfigurationManager();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ApartmentProfile>(), NullLoggerFactory.Instance).CreateMapper();

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
            new StripeServiceStub(),
            new FakeMomoService(),
            new NoOpIdentityVerificationService(),
            new RecordingWalletService(),
            configuration,
            mapper,
            null,
            null,
            checkTimeStateEventRepo);
    }

    private static BookingService CreateSut(Apartment sourceApartment, Apartment alternativeApartment, Booking booking)
    {
        var apartmentRepo = new InMemoryApartmentRepository(sourceApartment, alternativeApartment);
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
        var supportTicketRepo = new InMemoryRepository<SupportTicket>(s => s.TicketId);
        var paymentRepo = new InMemoryRepository<Payment>(p => p.PaymentId);
        var checkTimeStateEventRepo = new InMemoryRepository<BookingCheckTimeStateEvent>(e => e.EventId);
        var configuration = new ConfigurationManager();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ApartmentProfile>(), NullLoggerFactory.Instance).CreateMapper();

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
            new StripeServiceStub(),
            new FakeMomoService(),
            new NoOpIdentityVerificationService(),
            new RecordingWalletService(),
            configuration,
            mapper,
            null,
            null,
            checkTimeStateEventRepo);
    }
}