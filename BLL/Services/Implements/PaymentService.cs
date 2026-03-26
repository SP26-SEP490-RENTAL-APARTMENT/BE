using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PaymentService : BaseService<Payment>, IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(IPaymentRepository paymentRepository) : base(paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public Task<(IEnumerable<Payment> Items, int TotalCount)> GetLandlordPaymentsAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return _paymentRepository.GetByLandlordAsync(landlordId, page, pageSize, sortBy, sortOrder, fromDate, toDate);
    }
}
