using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using BLL.Services.Implements;
using Common.DTOs;
using Common.Enums;
using Common.Settings;
using DAL.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BLL.Tests;

public class BookingAvailabilityCalendarRefundTests
{
    [Fact]
    public async Task GetAvailabilityCalendarAsync_DoesNotMutateOverduePayOsBooking()
    {
        var fixture = CreateFixture();

        var calendar = await fixture.Service.GetAvailabilityCalendarAsync(fixture.ApartmentId);

        Assert.Equal(fixture.ApartmentId, calendar.ApartmentId);
        Assert.Equal("confirmed", fixture.Booking.Status);
        Assert.Empty(fixture.PaymentRepo.Items.Where(p => p.PaymentType == PaymentTypes.refund.ToString() && p.Status == PaymentStatus.success.ToString()));
        Assert.Equal("success", fixture.OriginalPayment.Status);
    }

    [Fact]
    public async Task ProcessCheckTimeAutomationAsync_ExpiresAndRefundsOverduePayOsBooking()
    {
        var fixture = CreateFixture();

        await fixture.Service.ProcessCheckTimeAutomationAsync();

        Assert.Equal("cancelled", fixture.Booking.Status);
        Assert.Single(fixture.PaymentRepo.Items.Where(p => p.PaymentType == PaymentTypes.refund.ToString() && p.Status == PaymentStatus.success.ToString()));
        Assert.Equal("refunded", fixture.OriginalPayment.Status);
    }

    [Fact]
    public async Task RecordCheckInAsync_ThrowsWhenRemainingBalanceExists()
    {
        var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.Service.RecordCheckInAsync(
                fixture.Booking.BookingId,
                new RecordCheckInDto
                {
                    ActualCheckIn = fixture.Booking.CheckInDate.ToDateTime(new TimeOnly(14, 0)),
                    PhotoEvidenceUrl = "https://example.com/checkin.jpg"
                },
                Guid.Empty));

        Assert.Equal("Remaining balance must be settled before check-in can be recorded.", exception.Message);
    }

    private static (BookingService Service, Guid ApartmentId, Guid LandlordId, Booking Booking, Payment OriginalPayment, InMemoryRepository<Payment> PaymentRepo) CreateFixture()
    {
        var apartmentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var now = Common.Utils.VietnamTime.Now;

        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = landlordId,
            Title = "Calendar test apartment",
            Status = "posted",
            BasePricePerNight = 1000000m
        };

        var booking = new Booking
        {
            BookingId = bookingId,
            ApartmentId = apartmentId,
            TenantId = tenantId,
            CheckInDate = DateOnly.FromDateTime(now.Date.AddDays(3)),
            CheckOutDate = DateOnly.FromDateTime(now.Date.AddDays(5)),
            Nights = 2,
            TotalPrice = 1000000m,
            AmountPaid = 300000m,
            RemainingAmount = 700000m,
            DepositAmount = 300000m,
            UpfrontPaymentAmount = 300000m,
            DepositPaid = true,
            BalanceDueDate = DateOnly.FromDateTime(now.Date.AddDays(-1)),
            Status = "confirmed",
            CreatedAt = now.AddDays(-2)
        };

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            RelatedEntityId = bookingId,
            RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
            Amount = 300000m,
            PaymentType = PaymentTypes.deposit.ToString(),
            PaymentPurpose = PaymentPurposes.booking_deposit.ToString(),
            LandlordId = landlordId,
            LandlordAmount = 210000m,
            PlatformFee = 90000m,
            SettlementStatus = PaymentStatus.success.ToString(),
            Method = "payos",
            Status = PaymentStatus.success.ToString(),
            TransactionId = "PAYOS-ORIGINAL-1",
            PaidAt = now.AddDays(-2),
            PayerBankBin = "970415",
            PayerAccountNumber = "0123456789"
        };

        var bookingRepo = new InMemoryBookingRepository(new[] { booking });
        var apartmentRepo = new InMemoryApartmentRepository(apartment);
        var bookingOfferRepo = new InMemoryBookingOfferRepository();
        var calendarRepo = new InMemoryApartmentPriceCalendarRepository();
        var packageRepo = new InMemoryRepository<Package>(p => p.PackageId);
        var notificationRepo = new InMemoryRepository<DAL.Models.Notification>(n => n.NotificationId);
        var bookingCheckTimeRepo = new InMemoryRepository<BookingCheckTime>(c => c.CheckTimeId);
        var temporaryResidenceRepo = new InMemoryRepository<TemporaryResidenceReport>(r => r.ReportId);
        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId);
        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User { UserId = tenantId, FullName = "Test Tenant" });
        var availabilityRepo = new InMemoryRepository<ApartmentAvailability>(a => a.AvailabilityId);
        var supportTicketRepo = new FakeSupportTicketRepository(new InMemoryRepository<SupportTicket>(s => s.TicketId));
        var paymentRepo = new InMemoryRepository<Payment>(p => p.PaymentId, payment);
        var checkTimeStateEventRepo = new InMemoryRepository<BookingCheckTimeStateEvent>(e => e.EventId);

        var sut = new BookingService(
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
            new FakePayOSClient(),
            new FakeMomoService(),
            Options.Create(new StripeSettings()),
            new FakePayOSPayoutService(),
            new NoOpIdentityVerificationService(),
            new RecordingWalletService(),
            new ConfigurationManager(),
            Options.Create(new BookingAdmissionPolicySettings()),
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper(),
            null,
            null,
            checkTimeStateEventRepo);

        return (sut, apartmentId, landlordId, booking, payment, paymentRepo);
    }
}