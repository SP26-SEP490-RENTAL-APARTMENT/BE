using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.Extensions.Options;
using MoMoApi;

namespace BLL.Services.Implements;

public class LandlordSubscriptionService : BaseService<LandlordSubscription>, ILandlordSubscriptionService
{
    private readonly ILandlordSubscriptionRepository _repository;
    private readonly IRepository<Landlord> _landlordRepository;
    private readonly ISubscriptionPlanService _subscriptionPlanService;
    private readonly IMomoService _momoService;
    private readonly IPaymentService _paymentService;
    private readonly IMomoTransactionService _momoTransactionService;
    private readonly MomoOptions _momoOptions;

    public LandlordSubscriptionService(
        ILandlordSubscriptionRepository repository,
        IRepository<Landlord> landlordRepository,
        ISubscriptionPlanService subscriptionPlanService,
        IMomoService momoService,
        IPaymentService paymentService,
        IMomoTransactionService momoTransactionService,
        IOptions<MomoOptions> momoOptions)
        : base(repository)
    {
        _repository = repository;
        _landlordRepository = landlordRepository;
        _subscriptionPlanService = subscriptionPlanService;
        _momoService = momoService;
        _paymentService = paymentService;
        _momoTransactionService = momoTransactionService;
        _momoOptions = momoOptions.Value;
    }

    public async Task<(IEnumerable<LandlordSubscription> Items, int TotalCount)> GetHistoryForLandlordAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return await _repository.GetByLandlordAsync(landlordId, page, pageSize, sortBy, sortOrder, search, fromDate, toDate);
    }

    public async Task<MomoCreatePaymentResponse> CreateMomoSubscriptionCheckoutAsync(
        Guid landlordId,
        StartLandlordSubscriptionRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId);
        if (landlord == null)
        {
            throw new InvalidOperationException("Landlord profile not found.");
        }

        var plan = await _subscriptionPlanService.GetByIdAsync(dto.PlanId);
        if (plan == null || plan.IsActive == false)
        {
            throw new InvalidOperationException("Subscription plan not found or inactive.");
        }

        var renewalType = (dto.RenewalType ?? RenewalType.monthly.ToString())
            .Trim()
            .ToLowerInvariant();

        if (renewalType != RenewalType.monthly.ToString() && renewalType != RenewalType.annual.ToString())
        {
            throw new ArgumentException("RenewalType must be 'monthly' or 'annual'.");
        }

        decimal amount;
        if (renewalType == RenewalType.annual.ToString())
        {
            if (!plan.PriceAnnual.HasValue || plan.PriceAnnual.Value <= 0)
            {
                throw new InvalidOperationException("Annual price is not configured for this plan.");
            }
            amount = plan.PriceAnnual.Value;
        }
        else
        {
            if (plan.PriceMonthly <= 0)
            {
                throw new InvalidOperationException("Monthly price is not configured for this plan.");
            }
            amount = plan.PriceMonthly;
        }

        var landlordSubscription = new LandlordSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            LandlordId = landlord.LandlordId,
            PlanId = plan.PlanId,
            Status = Status.pending_payment.ToString(),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = null,
            RenewalType = renewalType,
            AutoRenew = dto.AutoRenew,
            PaymentMethod = "momo_wallet",
            LastPaymentId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        landlordSubscription = await CreateAsync(landlordSubscription);

        var paymentPurpose = renewalType == RenewalType.annual.ToString()
            ? PaymentPurposes.subscription_annual.ToString()
            : PaymentPurposes.subscription_monthly.ToString();

        var payment = new Payment
        {
            Amount = amount,
            PaymentType = PaymentTypes.deposit.ToString(),
            PaymentPurpose = paymentPurpose,
            RelatedEntityId = landlordSubscription.SubscriptionId,
            RelatedEntityType = PaymentRelatedEntityType.host_subscription.ToString(),
            Method = "momo_wallet",
            Status = PaymentStatus.pending.ToString()
        };

        var momoRequest = new MomoCreatePaymentRequest
        {
            Amount = (long)amount,
            OrderInfo = $"Subscription {landlordSubscription.SubscriptionId} for landlord {landlord.LandlordId}",
            ExtraData = landlordSubscription.SubscriptionId.ToString(),
            PaymentType = payment.PaymentType,
            PaymentPurpose = payment.PaymentPurpose
        };

        var momoResult = await _momoService.CreateWalletPaymentAsync(momoRequest, cancellationToken);

        if (momoResult.ResultCode != 0)
        {
            throw new InvalidOperationException($"MoMo payment creation failed: {momoResult.Message}");
        }

        await _paymentService.CreateAsync(payment);

        landlordSubscription.LastPaymentId = payment.PaymentId;
        landlordSubscription.UpdatedAt = DateTime.UtcNow;
        await UpdateAsync(landlordSubscription);

        var requestLog = new MomoTransaction
        {
            RequestId = momoResult.RequestId,
            PartnerCode = _momoOptions.PartnerCode,
            Amount = momoRequest.Amount,
            Type = "create_wallet_payment_subscription",
            RequestBody = momoResult.RequestRaw ?? string.Empty,
            ResponseBody = momoResult.ResponseRaw ?? string.Empty,
            Status = "pending",
            ResultCode = momoResult.ResultCode,
            Message = momoResult.Message,
            PaymentId = payment.PaymentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _momoTransactionService.CreateAsync(requestLog);

        return momoResult;
    }
}
