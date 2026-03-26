using Common.DTOs;
using DAL.Models;
using MoMoApi;

namespace BLL.Services.Interfaces;

public interface ILandlordSubscriptionService : IBaseService<LandlordSubscription>
{
	Task<(IEnumerable<LandlordSubscription> Items, int TotalCount)> GetHistoryForLandlordAsync(
		Guid landlordId,
		int page,
		int pageSize,
		string? sortBy = null,
		string? sortOrder = null,
		string? search = null,
		DateTime? fromDate = null,
		DateTime? toDate = null);

	Task<MomoCreatePaymentResponse> CreateMomoSubscriptionCheckoutAsync(
		Guid landlordId,
		StartLandlordSubscriptionRequestDto dto,
		CancellationToken cancellationToken = default);
}
