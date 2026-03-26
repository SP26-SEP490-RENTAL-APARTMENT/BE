using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IPaymentService : IBaseService<Payment>
{
	Task<(IEnumerable<Payment> Items, int TotalCount)> GetLandlordPaymentsAsync(
		Guid landlordId,
		int page,
		int pageSize,
		string? sortBy = null,
		string? sortOrder = null,
		DateTime? fromDate = null,
		DateTime? toDate = null);
}
