using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;

namespace BLL.Services.Implements;

public sealed class ReviewService : BaseService<Review>, IReviewService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IReviewRepository _reviewRepository;

    public ReviewService(
        IReviewRepository reviewRepository,
        IBookingRepository bookingRepository,
        IApartmentRepository apartmentRepository)
        : base(reviewRepository)
    {
        _reviewRepository = reviewRepository;
        _bookingRepository = bookingRepository;
        _apartmentRepository = apartmentRepository;
    }

    public override async Task<(IEnumerable<Review> Items, int TotalCount)> GetAllAsync(
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
            "ReviewId",
            "BookingId",
            "ReviewerId",
            "ReviewedId",
            "ApartmentId",
            "Rating",
            "CommentEn",
            "CommentVi",
            "CreatedAt"
        };

        return await _reviewRepository.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
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

        // var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);
        // if (today < booking.CheckOutDate)
        // {
        //     throw new InvalidOperationException("You can only review after your check-out date.");
        // }

        // if (!string.IsNullOrWhiteSpace(booking.Status) &&
        //     string.Equals(booking.Status, BookingStatus.cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
        // {
        //     throw new InvalidOperationException("You cannot review a cancelled booking.");
        // }

        if (apartmentId.HasValue && apartmentId.Value != booking.ApartmentId)
        {
            throw new InvalidOperationException("Apartment does not match the booking.");
        }

        var existingReviews = await _reviewRepository.FindAsync(r => r.BookingId == bookingId && r.ReviewerId == reviewerUserId);
        if (existingReviews.Any())
        {
            throw new InvalidOperationException("You have already reviewed this booking.");
        }
    }

    public async Task<(Guid ApartmentId, Guid LandlordId)> GetReviewTargetsByBookingIdAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
        {
            throw new ArgumentException("Booking not found.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId);
        if (apartment == null)
        {
            throw new InvalidOperationException("Apartment not found for this booking.");
        }

        return (booking.ApartmentId, apartment.LandlordId);
    }

    public async Task<(double? AverageRating, int TotalReviews)> GetApartmentAverageRatingAsync(Guid apartmentId)
    {
        var reviews = await _reviewRepository.FindAsync(r => r.ApartmentId == apartmentId && r.Rating != null);
        var reviewList = reviews.ToList();

        var totalReviews = reviewList.Count;
        if (totalReviews == 0)
        {
            return (null, 0);
        }

        var average = reviewList.Average(r => (double)r.Rating!.Value);
        return (average, totalReviews);
    }

    public async Task<IEnumerable<Review>> GetByApartmentIdAsync(Guid apartmentId)
    {
        return await _reviewRepository.FindAsync(r => r.ApartmentId == apartmentId);
    }

    public async Task<IEnumerable<Review>> GetByReviewerIdAsync(Guid reviewerId)
    {
        return await _reviewRepository.FindAsync(r => r.ReviewerId == reviewerId);
    }
}
