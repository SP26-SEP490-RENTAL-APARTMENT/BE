using Common.DTOs;
using DAL.Models;
using MoMoApi;
using PayOS.Models.V2.PaymentRequests;
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

	Task<(SubscriptionPlan plan, string renewalType, decimal amount)> ResolvePlanAndAmountAsync(
		StartLandlordSubscriptionRequestDto dto);

	Task<Common.DTOs.PayOsCreatePaymentResponse> CreatePayOsSubscriptionCheckoutAsync(
		Guid landlordId,
		StartLandlordSubscriptionRequestDto dto,
		CreatePaymentLinkRequest payosRequest,
		CreatePaymentLinkResponse payosResult,
		CancellationToken cancellationToken = default);

	Task<WalletSubscriptionPaymentResponseDto> PaySubscriptionByWalletAsync(
		Guid landlordId,
		StartLandlordSubscriptionRequestDto dto,
		CancellationToken cancellationToken = default);
}
