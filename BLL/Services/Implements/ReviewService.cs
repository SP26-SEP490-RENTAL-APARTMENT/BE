using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;

namespace BLL.Services.Implements;

public sealed class ReviewService : BaseService<Review>, IReviewService
{
    private readonly IBookingRepository _bookingRepository;

    public ReviewService(IRepository<Review> repository, IBookingRepository bookingRepository)
        : base(repository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task ValidateTenantReviewEligibilityAsync(Guid bookingId, Guid reviewerUserId, Guid? apartmentId = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
        {
            throw new ArgumentException("Booking not found.");
        }

        if (booking.TenantId != reviewerUserId)
        {
            throw new InvalidOperationException("You can only review your own bookings.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today < booking.CheckOutDate)
        {
            throw new InvalidOperationException("You can only review after your check-out date.");
        }

        if (!string.IsNullOrWhiteSpace(booking.Status) &&
            string.Equals(booking.Status, BookingStatus.cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("You cannot review a cancelled booking.");
        }

        if (apartmentId.HasValue && apartmentId.Value != booking.ApartmentId)
        {
            throw new InvalidOperationException("Apartment does not match the booking.");
        }

        var existingReviews = await _repository.FindAsync(r => r.BookingId == bookingId && r.ReviewerId == reviewerUserId);
        if (existingReviews.Any())
        {
            throw new InvalidOperationException("You have already reviewed this booking.");
        }
    }

    public async Task<(double? AverageRating, int TotalReviews)> GetApartmentAverageRatingAsync(Guid apartmentId)
    {
        var reviews = await _repository.FindAsync(r => r.ApartmentId == apartmentId && r.Rating != null);
        var reviewList = reviews.ToList();

        var totalReviews = reviewList.Count;
        if (totalReviews == 0)
        {
            return (null, 0);
        }

        var average = reviewList.Average(r => (double)r.Rating!.Value);
        return (average, totalReviews);
    }
}
