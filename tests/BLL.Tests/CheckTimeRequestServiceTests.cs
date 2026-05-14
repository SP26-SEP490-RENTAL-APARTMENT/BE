using BLL.Services.Implements;
using BLL.Services.Interfaces;
using AutoMapper;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BLL.Tests;

public class CheckTimeRequestServiceTests
{
    private readonly Mock<ICheckTimeRequestRepository> _mockCheckTimeRequestRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<IApartmentRepository> _mockApartmentRepository;
    private readonly Mock<IRepository<DAL.Models.Notification>> _mockNotificationRepository;
    private readonly Mock<IRepository<Booking>> _mockBookingBaseRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly ICheckTimeRequestService _service;

    public CheckTimeRequestServiceTests()
    {
        _mockCheckTimeRequestRepository = new Mock<ICheckTimeRequestRepository>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockApartmentRepository = new Mock<IApartmentRepository>();
        _mockNotificationRepository = new Mock<IRepository<DAL.Models.Notification>>();
        _mockBookingBaseRepository = new Mock<IRepository<Booking>>();
        _mockMapper = new Mock<IMapper>();

        _service = new CheckTimeRequestService(
            _mockCheckTimeRequestRepository.Object,
            _mockBookingRepository.Object,
            _mockApartmentRepository.Object,
            _mockNotificationRepository.Object,
            _mockBookingBaseRepository.Object,
            null!,
            _mockMapper.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsSuccessfully()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();

        var booking = new Booking
        {
            BookingId = bookingId,
            TenantId = guestId,
            ApartmentId = apartmentId,
            Status = "Confirmed"
        };

        var apartment = new Apartment { ApartmentId = apartmentId, LandlordId = landlordId };

        var dto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(2),
            GuestReason = "Flight arrives early"
        };

        _mockBookingRepository
            .Setup(r => r.GetByIdAsync(bookingId))
            .ReturnsAsync(booking);

        _mockCheckTimeRequestRepository
            .Setup(r => r.HasPendingOrCounterOfferAsync(bookingId, "EarlyCheckIn"))
            .ReturnsAsync(false);

        _mockApartmentRepository
            .Setup(r => r.GetByIdAsync(apartmentId))
            .ReturnsAsync(apartment);

        _mockCheckTimeRequestRepository
            .Setup(r => r.AddAsync(It.IsAny<CheckTimeRequest>()))
            .Returns(Task.CompletedTask);

        _mockCheckTimeRequestRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockNotificationRepository
            .Setup(r => r.AddAsync(It.IsAny<DAL.Models.Notification>()))
            .Returns(Task.CompletedTask);

        _mockNotificationRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockMapper
            .Setup(m => m.Map<CheckTimeRequestResponseDto>(It.IsAny<CheckTimeRequest>()))
            .Returns((CheckTimeRequest src) => new CheckTimeRequestResponseDto
            {
                Id = src.Id,
                BookingId = src.BookingId,
                RequestType = src.RequestType,
                Status = src.Status
            });

        // Act
        var result = await _service.CreateAsync(bookingId, guestId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EarlyCheckIn", result.RequestType);
        Assert.Equal("Pending", result.Status);
        _mockCheckTimeRequestRepository.Verify(r => r.AddAsync(It.IsAny<CheckTimeRequest>()), Times.Once);
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<DAL.Models.Notification>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePending_Throws()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var booking = new Booking { BookingId = bookingId, TenantId = guestId, Status = "Confirmed" };

        var dto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(2)
        };

        _mockBookingRepository
            .Setup(r => r.GetByIdAsync(bookingId))
            .ReturnsAsync(booking);

        _mockCheckTimeRequestRepository
            .Setup(r => r.HasPendingOrCounterOfferAsync(bookingId, "EarlyCheckIn"))
            .ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(bookingId, guestId, dto));
        Assert.Contains("already has a pending", ex.Message);
    }

    [Fact]
    public async Task CounterOfferAsync_Success()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();

        var booking = new Booking { BookingId = bookingId, TenantId = tenantId, ApartmentId = apartmentId };
        var apartment = new Apartment { ApartmentId = apartmentId, LandlordId = hostId };

        var request = new CheckTimeRequest
        {
            Id = requestId,
            BookingId = bookingId,
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(2),
            Status = "Pending",
            Booking = booking
        };

        var dto = new CounterCheckTimeDto
        {
            CounterOfferedTime = DateTime.UtcNow.AddHours(1),
            CounterOfferedFee = 100000,
            HostResponse = "Can do 10am"
        };

        _mockCheckTimeRequestRepository
            .Setup(r => r.FindByIdWithBookingAsync(requestId))
            .ReturnsAsync(request);

        _mockApartmentRepository
            .Setup(r => r.GetByIdAsync(apartmentId))
            .ReturnsAsync(apartment);

        _mockCheckTimeRequestRepository
            .Setup(r => r.Update(It.IsAny<CheckTimeRequest>()))
            .Callback<CheckTimeRequest>(req => req.Status = "CounterOffered");

        _mockCheckTimeRequestRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockNotificationRepository
            .Setup(r => r.AddAsync(It.IsAny<DAL.Models.Notification>()))
            .Returns(Task.CompletedTask);

        _mockNotificationRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockMapper
            .Setup(m => m.Map<CheckTimeRequestResponseDto>(It.IsAny<CheckTimeRequest>()))
            .Returns(new CheckTimeRequestResponseDto { Status = "CounterOffered" });

        // Act
        var result = await _service.CounterOfferAsync(requestId, hostId, dto);

        // Assert
        Assert.Equal("CounterOffered", result.Status);
        _mockCheckTimeRequestRepository.Verify(r => r.Update(It.IsAny<CheckTimeRequest>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_WithConflictingBooking_Throws()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();

        var requestedTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10);

        var booking = new Booking { BookingId = bookingId, TenantId = tenantId, ApartmentId = apartmentId };
        var apartment = new Apartment { ApartmentId = apartmentId, LandlordId = hostId };

        var request = new CheckTimeRequest
        {
            Id = requestId,
            BookingId = bookingId,
            RequestType = "EarlyCheckIn",
            RequestedTime = requestedTime,
            Status = "Pending",
            Booking = booking
        };

        var overlappingBooking = new Booking
        {
            BookingId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            BookingCheckTime = new BookingCheckTime
            {
                ScheduledCheckOut = requestedTime.AddHours(-1),
                ActualCheckOut = null
            }
        };

        var dto = new ApproveCheckTimeDto { AgreedFee = 0 };

        _mockCheckTimeRequestRepository
            .Setup(r => r.FindByIdWithBookingAsync(requestId))
            .ReturnsAsync(request);

        _mockApartmentRepository
            .Setup(r => r.GetByIdAsync(apartmentId))
            .ReturnsAsync(apartment);

        _mockBookingRepository
            .Setup(r => r.GetByIdAsync(bookingId))
            .ReturnsAsync(booking);

        _mockCheckTimeRequestRepository
            .Setup(r => r.FindOverlappingBookingsAsync(apartmentId, requestedTime, "EarlyCheckIn"))
            .ReturnsAsync(new[] { overlappingBooking });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveAsync(requestId, hostId, dto));
        Assert.Contains("conflicting bookings", ex.Message);
    }

    [Fact]
    public async Task CanApproveAsync_NoConflict_ReturnsTrue()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();
        var requestedTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10);

        var booking = new Booking { BookingId = bookingId, ApartmentId = apartmentId };
        var apartment = new Apartment { ApartmentId = apartmentId };

        _mockBookingRepository
            .Setup(r => r.GetByIdAsync(bookingId))
            .ReturnsAsync(booking);

        _mockApartmentRepository
            .Setup(r => r.GetByIdAsync(apartmentId))
            .ReturnsAsync(apartment);

        _mockCheckTimeRequestRepository
            .Setup(r => r.FindOverlappingBookingsAsync(apartmentId, requestedTime, "EarlyCheckIn"))
            .ReturnsAsync(Enumerable.Empty<Booking>());

        // Act
        var result = await _service.CanApproveAsync(bookingId, "EarlyCheckIn", requestedTime);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RejectAsync_Success()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var apartmentId = Guid.NewGuid();

        var booking = new Booking { BookingId = bookingId, TenantId = tenantId, ApartmentId = apartmentId };
        var apartment = new Apartment { ApartmentId = apartmentId, LandlordId = hostId };

        var request = new CheckTimeRequest
        {
            Id = requestId,
            BookingId = bookingId,
            Status = "Pending",
            Booking = booking
        };

        var dto = new RejectCheckTimeDto { Reason = "Unable to accommodate" };

        _mockCheckTimeRequestRepository
            .Setup(r => r.FindByIdWithBookingAsync(requestId))
            .ReturnsAsync(request);

        _mockApartmentRepository
            .Setup(r => r.GetByIdAsync(apartmentId))
            .ReturnsAsync(apartment);

        _mockCheckTimeRequestRepository
            .Setup(r => r.Update(It.IsAny<CheckTimeRequest>()))
            .Callback<CheckTimeRequest>(req => req.Status = "Rejected");

        _mockCheckTimeRequestRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockNotificationRepository
            .Setup(r => r.AddAsync(It.IsAny<DAL.Models.Notification>()))
            .Returns(Task.CompletedTask);

        _mockNotificationRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockMapper
            .Setup(m => m.Map<CheckTimeRequestResponseDto>(It.IsAny<CheckTimeRequest>()))
            .Returns(new CheckTimeRequestResponseDto { Status = "Rejected" });

        // Act
        var result = await _service.RejectAsync(requestId, hostId, dto);

        // Assert
        Assert.Equal("Rejected", result.Status);
    }
}
