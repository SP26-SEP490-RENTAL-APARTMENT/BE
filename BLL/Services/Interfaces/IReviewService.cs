using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IReviewService : IBaseService<Review>
{
	/// <summary>
	/// Validates whether the given user is allowed to create a review for the specified booking.
	/// Throws an exception when the review is not allowed.
	/// </summary>
	/// <param name="bookingId">The booking identifier linked to the stay.</param>
	/// <param name="reviewerUserId">The authenticated user id attempting to review.</param>
	/// <param name="apartmentId">Optional apartment id passed from client for consistency checks.</param>
	Task ValidateTenantReviewEligibilityAsync(Guid bookingId, Guid reviewerUserId, Guid? apartmentId = null);

	/// <summary>
	/// Gets apartment and landlord identifiers associated with a booking for review creation.
	/// </summary>
	/// <param name="bookingId">The booking identifier.</param>
	Task<(Guid ApartmentId, Guid LandlordId)> GetReviewTargetsByBookingIdAsync(Guid bookingId);

	/// <summary>
	/// Calculates the average rating and total number of reviews for a given apartment.
	/// Only reviews with a non-null rating are considered.
	/// </summary>
	/// <param name="apartmentId">The apartment identifier.</param>
	/// <returns>A tuple containing the average rating (or null if no reviews) and total review count.</returns>
	Task<(double? AverageRating, int TotalReviews)> GetApartmentAverageRatingAsync(Guid apartmentId);

	/// <summary>
	/// Gets all reviews that belong to a specific apartment.
	/// </summary>
	/// <param name="apartmentId">The apartment identifier.</param>
	Task<IEnumerable<Review>> GetByApartmentIdAsync(Guid apartmentId);

	/// <summary>
	/// Gets all reviews created by a specific reviewer.
	/// </summary>
	/// <param name="reviewerId">The reviewer user identifier.</param>
	Task<IEnumerable<Review>> GetByReviewerIdAsync(Guid reviewerId);
}
