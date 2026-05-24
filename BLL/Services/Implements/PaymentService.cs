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

    public Task<decimal> GetLandlordRevenueTotalAsync(
        Guid landlordId,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return _paymentRepository.GetLandlordRevenueTotalAsync(landlordId, fromDate, toDate);
    }

    public Task<(IEnumerable<Payment> Items, int TotalCount)> GetLandlordPaymentsAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Dictionary<string, string>? filters = null)
    {
        var allowedColumns = new[] { "PaymentId", "RelatedEntityId", "Amount", "PaymentType", "PaymentPurpose", "RelatedEntityType", "LandlordId", "LandlordAmount", "PlatformFee", "SettlementStatus", "Method", "Status", "TransactionId", "PaidAt" };
        return _paymentRepository.GetByLandlordAsync(landlordId, page, pageSize, sortBy, sortOrder, fromDate, toDate, filters, allowedColumns);
    }

    public Task<(IEnumerable<Payment> Items, int TotalCount)> GetTenantPaymentsAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Dictionary<string, string>? filters = null)
    {
        var allowedColumns = new[] { "PaymentId", "RelatedEntityId", "Amount", "PaymentType", "PaymentPurpose", "RelatedEntityType", "LandlordId", "LandlordAmount", "PlatformFee", "SettlementStatus", "Method", "Status", "TransactionId", "PaidAt" };
        return _paymentRepository.GetByTenantAsync(tenantId, page, pageSize, sortBy, sortOrder, fromDate, toDate, filters, allowedColumns);
    }
}
