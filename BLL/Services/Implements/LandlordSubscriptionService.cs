using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Enums;
using Common.Settings;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.Extensions.Options;
using MoMoApi;
using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace BLL.Services.Implements;

public class LandlordSubscriptionService : BaseService<LandlordSubscription>, ILandlordSubscriptionService
{
    private readonly ILandlordSubscriptionRepository _repository;
    private readonly IRepository<Landlord> _landlordRepository;
    private readonly ISubscriptionPlanService _subscriptionPlanService;
    private readonly IMomoService _momoService;
    private readonly PayOSClient _payOsClient;
    private readonly IPaymentService _paymentService;
    private readonly IMomoTransactionService _momoTransactionService;
    private readonly ILandlordWalletService _landlordWalletService;
    private readonly MomoOptions _momoOptions;
    private readonly StripeSettings _stripeSettings;

    public LandlordSubscriptionService(
        ILandlordSubscriptionRepository repository,
        IRepository<Landlord> landlordRepository,
        ISubscriptionPlanService subscriptionPlanService,
        IMomoService momoService,
        IPaymentService paymentService,
        IMomoTransactionService momoTransactionService,
        ILandlordWalletService landlordWalletService,
        PayOSClient payOsClient,
        IOptions<MomoOptions> momoOptions,
        IOptions<StripeSettings> stripeSettings)
        : base(repository)
    {
        _repository = repository;
        _landlordRepository = landlordRepository;
        _subscriptionPlanService = subscriptionPlanService;
        _momoService = momoService;
        _paymentService = paymentService;
        _momoTransactionService = momoTransactionService;
        _landlordWalletService = landlordWalletService;
        _payOsClient = payOsClient;
        _momoOptions = momoOptions.Value;
        _stripeSettings = stripeSettings.Value;
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
            StartDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now),
            EndDate = null,
            RenewalType = renewalType,
            AutoRenew = dto.AutoRenew,
            PaymentMethod = "momo_wallet",
            LastPaymentId = null,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
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
            PaymentPurpose = payment.PaymentPurpose,
            RedirectUrl = "http://localhost:5173/landlord/my-subscriptions"
        };

        var momoResult = await _momoService.CreateWalletPaymentAsync(momoRequest, cancellationToken);

        if (momoResult.ResultCode != 0)
        {
            throw new InvalidOperationException($"MoMo payment creation failed: {momoResult.Message}");
        }

        await _paymentService.CreateAsync(payment);

        landlordSubscription.LastPaymentId = payment.PaymentId;
        landlordSubscription.UpdatedAt = Common.Utils.VietnamTime.Now;
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
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };

        await _momoTransactionService.CreateAsync(requestLog);

        return momoResult;
    }

    public async Task<Common.DTOs.PayOsCreatePaymentResponse> CreatePayOsSubscriptionCheckoutAsync(
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
            StartDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now),
            EndDate = null,
            RenewalType = renewalType,
            AutoRenew = dto.AutoRenew,
            PaymentMethod = "payos",
            LastPaymentId = null,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
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
            Method = "payos",
            Status = PaymentStatus.pending.ToString()
        };

        var payosRequest = new CreatePaymentLinkRequest
        {
            OrderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Amount = (long)amount,
            Description = "Subscription payment",
            ReturnUrl = _stripeSettings.SuccessUrl,
            CancelUrl = _stripeSettings.CancelUrl,
            Items = new List<PaymentLinkItem>
            {
                new PaymentLinkItem
                {
                    Name = plan.Name,
                    Quantity = 1,
                    Price = (long)amount,
                    Unit = "subscription"
                }
            }
        };

        CreatePaymentLinkResponse payosResult;
        try
        {
            payosResult = await _payOsClient.PaymentRequests.CreateAsync(payosRequest);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"PayOS payment creation failed: {ex.Message}");
        }

        await _paymentService.CreateAsync(payment);

        payment.TransactionId = payosResult.PaymentLinkId;
        await _paymentService.UpdateAsync(payment);

        await _momoTransactionService.CreateAsync(new MomoTransaction
        {
            RequestId = payosResult.PaymentLinkId ?? Guid.NewGuid().ToString(),
            PartnerCode = string.Empty,
            Amount = payosResult.Amount,
            Type = "create_subscription_payment_payos",
            RequestBody = System.Text.Json.JsonSerializer.Serialize(payosRequest),
            ResponseBody = System.Text.Json.JsonSerializer.Serialize(payosResult),
            Status = "pending",
            ResultCode = null,
            Message = payosResult.Status.ToString(),
            PaymentId = payment.PaymentId,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        });

        landlordSubscription.LastPaymentId = payment.PaymentId;
        landlordSubscription.UpdatedAt = Common.Utils.VietnamTime.Now;
        await UpdateAsync(landlordSubscription);

        return new PayOsCreatePaymentResponse
        {
            Success = true,
            Message = payosResult.Status.ToString(),
            Url = payosResult.CheckoutUrl,
            QrCodeUrl = payosResult.QrCode,
            OrderId = payosResult.PaymentLinkId ?? string.Empty,
            RequestId = payosResult.PaymentLinkId ?? string.Empty,
            Amount = (long)amount,
            RequestRaw = System.Text.Json.JsonSerializer.Serialize(payosRequest),
            ResponseRaw = System.Text.Json.JsonSerializer.Serialize(payosResult)
        };
    }

    public async Task<WalletSubscriptionPaymentResponseDto> PaySubscriptionByWalletAsync(
        Guid landlordId,
        StartLandlordSubscriptionRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId)
            ?? throw new InvalidOperationException("Landlord profile not found.");

        var (plan, renewalType, amount) = await ResolvePlanAndAmountAsync(dto);

        var subscription = new LandlordSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            LandlordId = landlord.LandlordId,
            PlanId = plan.PlanId,
            Status = Status.pending_payment.ToString(),
            StartDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now),
            EndDate = null,
            RenewalType = renewalType,
            AutoRenew = dto.AutoRenew,
            PaymentMethod = "landlord_wallet",
            LastPaymentId = null,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        };

        subscription = await CreateAsync(subscription);

        try
        {
            await _landlordWalletService.DebitAvailableAsync(landlordId, amount);

            var paymentPurpose = renewalType == RenewalType.annual.ToString()
                ? PaymentPurposes.subscription_annual.ToString()
                : PaymentPurposes.subscription_monthly.ToString();

            var payment = new Payment
            {
                Amount = amount,
                PaymentType = PaymentTypes.deposit.ToString(),
                PaymentPurpose = paymentPurpose,
                RelatedEntityId = subscription.SubscriptionId,
                RelatedEntityType = PaymentRelatedEntityType.host_subscription.ToString(),
                Method = "landlord_wallet",
                Status = PaymentStatus.success.ToString(),
                PaidAt = Common.Utils.VietnamTime.Now,
                TransactionId = $"wallet_sub_{subscription.SubscriptionId:N}"
            };

            payment = await _paymentService.CreateAsync(payment);

            ApplyActivatedSubscriptionState(subscription, payment.PaymentId);
            await UpdateAsync(subscription);

            landlord.CurrentPlanId = subscription.PlanId;
            landlord.SubscriptionStatus = SubscriptionStatus.active.ToString();
            landlord.SubscriptionExpiresAt = subscription.EndDate;
            _landlordRepository.Update(landlord);
            await _landlordRepository.SaveChangesAsync();

            var wallet = await _landlordWalletService.GetOrCreateAsync(landlordId);
            return new WalletSubscriptionPaymentResponseDto
            {
                SubscriptionId = subscription.SubscriptionId,
                PaymentId = payment.PaymentId,
                Amount = amount,
                RenewalType = renewalType,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                SubscriptionStatus = subscription.Status,
                RemainingWalletBalance = wallet.AvailableBalance
            };
        }
        catch
        {
            subscription.Status = Status.cancelled.ToString();
            subscription.UpdatedAt = Common.Utils.VietnamTime.Now;
            await UpdateAsync(subscription);
            throw;
        }
    }

    private async Task<(SubscriptionPlan plan, string renewalType, decimal amount)> ResolvePlanAndAmountAsync(StartLandlordSubscriptionRequestDto dto)
    {
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

        return (plan, renewalType, amount);
    }

    private static void ApplyActivatedSubscriptionState(LandlordSubscription subscription, Guid paymentId)
    {
        var nowDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);
        var months = string.Equals(subscription.RenewalType, RenewalType.annual.ToString(), StringComparison.OrdinalIgnoreCase) ? 12 : 1;

        subscription.Status = Status.active.ToString();
        subscription.StartDate = nowDate;
        subscription.EndDate = nowDate.AddMonths(months);
        subscription.LastPaymentId = paymentId;
        subscription.UpdatedAt = Common.Utils.VietnamTime.Now;
    }
}
