using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoMoApi;
using Short_termApartmentAPI.Controllers;

namespace Short_termApartmentAPI.IntegrationTests;

public class ControllerBehaviorTests
{
    [Fact]
    public async Task LandlordController_GetPaymentHistory_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var controller = CreateLandlordController();
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        var result = await controller.GetPaymentHistory();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task LandlordController_GetPaymentHistory_ReturnsNotFound_WhenLandlordIsMissing()
    {
        var controller = CreateLandlordController();
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var result = await controller.GetPaymentHistory();

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task LandlordController_GetPaymentHistory_ReturnsMappedPayments()
    {
        var landlordId = Guid.NewGuid();
        var controller = CreateLandlordController(
            landlordService: new LandlordServiceStub
            {
                LandlordByUserId = new Landlord { LandlordId = landlordId, LandlordNavigation = new User { UserId = landlordId } }
            },
            paymentService: new PaymentServiceStub
            {
                LandlordPayments = new List<Payment>
                {
                    new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        Amount = 125000m,
                        PaymentType = "deposit",
                        PaymentPurpose = "booking_deposit",
                        Method = "momo_wallet",
                        Status = "success",
                        TransactionId = "txn-1"
                    }
                }
            });
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, landlordId.ToString()));

        var result = await controller.GetPaymentHistory();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = GetProperty<IEnumerable<PaymentHistoryDto>>(ok.Value!, "Items");
        var totalCount = GetProperty<int>(ok.Value!, "TotalCount");

        Assert.Equal(1, totalCount);
        var dto = Assert.Single(items);
        Assert.Equal(125000m, dto.Amount);
        Assert.Equal("success", dto.Status);
        Assert.Equal("txn-1", dto.TransactionId);
    }

    [Fact]
    public async Task MomoController_Ipn_ReturnsBadRequest_WhenSignatureIsInvalid()
    {
        var controller = CreateMomoController(momoService: new MomoServiceStub { ValidateDisbursementSignature = false });
        SetRequestBody(controller, "{\"signature\":\"bad\"}");

        var result = await controller.Ipn();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MomoController_DisbursementIpn_ReturnsBadRequest_WhenSignatureIsInvalid()
    {
        var controller = CreateMomoController(momoService: new MomoServiceStub { ValidateDisbursementSignature = false });
        SetRequestBody(controller, "{\"signature\":\"bad\"}");

        var result = await controller.DisbursementIpn();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TenantController_GetPaymentHistory_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var controller = CreateTenantController();
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        var result = await controller.GetPaymentHistory();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task TenantController_GetPaymentById_ReturnsNotFound_WhenPaymentMissing()
    {
        var controller = CreateTenantController(paymentService: new PaymentServiceStub());
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var result = await controller.GetPaymentById(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task TenantController_GetPaymentById_ReturnsNotFound_WhenBookingBelongsToAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var paymentService = new PaymentServiceStub();
        paymentService.PaymentsById[paymentId] = new Payment
        {
            PaymentId = paymentId,
            RelatedEntityType = "booking",
            RelatedEntityId = bookingId,
            Amount = 1000m,
            PaymentType = "deposit",
            PaymentPurpose = "booking_deposit",
            Method = "momo_wallet"
        };

        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking
            {
                BookingId = bookingId,
                TenantId = otherTenantId
            }
        };

        var controller = CreateTenantController(paymentService: paymentService, bookingService: bookingService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()));

        var result = await controller.GetPaymentById(paymentId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task TenantController_GetPaymentById_ReturnsOk_WhenBookingBelongsToTenant()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var paymentService = new PaymentServiceStub();
        paymentService.PaymentsById[paymentId] = new Payment
        {
            PaymentId = paymentId,
            RelatedEntityType = "booking",
            RelatedEntityId = bookingId,
            Amount = 777m,
            PaymentType = "deposit",
            PaymentPurpose = "booking_deposit",
            Method = "momo_wallet",
            Status = "success",
            TransactionId = "txn-tenant-1"
        };

        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking
            {
                BookingId = bookingId,
                TenantId = tenantId
            }
        };

        var controller = CreateTenantController(paymentService: paymentService, bookingService: bookingService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()));

        var result = await controller.GetPaymentById(paymentId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<Short_termApartmentAPI.Middlewares.ApiResponse<PaymentHistoryDto>>(ok.Value);
        Assert.Equal(777m, response.Data.Amount);
        Assert.Equal("txn-tenant-1", response.Data.TransactionId);
    }

    [Fact]
    public async Task MomoController_Ipn_TriggersDepositSideEffect_EvenWhenPaymentAlreadySuccess()
    {
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentService = new PaymentServiceStub();
        paymentService.PaymentsById[paymentId] = new Payment
        {
            PaymentId = paymentId,
            RelatedEntityType = "booking",
            RelatedEntityId = bookingId,
            PaymentType = "deposit",
            PaymentPurpose = "booking_deposit",
            Method = "momo_wallet",
            Amount = 1000m,
            Status = "success"
        };

        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking
            {
                BookingId = bookingId,
                TenantId = Guid.NewGuid(),
                DepositPaid = false,
                Status = "pending"
            }
        };

        var txService = new MomoTransactionServiceStub();
        txService.ByRequestId["REQ-IPN-1"] = new MomoTransaction
        {
            RequestId = "REQ-IPN-1",
            PaymentId = paymentId,
            Status = "pending",
            PartnerCode = "PARTNER",
            Amount = 1000,
            Type = "create_wallet_payment",
            RequestBody = "{}",
            ResponseBody = "{}",
            Message = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var controller = CreateMomoController(paymentService: paymentService, bookingService: bookingService, momoTransactionService: txService);

        var payload = BuildValidMomoIpnPayload(
            accessKey: "ACCESS",
            secretKey: "SECRET",
            requestId: "REQ-IPN-1",
            orderId: "ORD-IPN-1",
            resultCode: 0);
        SetRequestBody(controller, payload);

        var result = await controller.Ipn();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, bookingService.MarkDepositPaidCalls);
    }

    [Fact]
    public async Task MomoController_Ipn_TriggersUpfrontPaymentSideEffect_EvenWhenPaymentAlreadySuccess()
    {
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentService = new PaymentServiceStub();
        paymentService.PaymentsById[paymentId] = new Payment
        {
            PaymentId = paymentId,
            RelatedEntityType = "booking",
            RelatedEntityId = bookingId,
            PaymentType = "upfront",
            PaymentPurpose = "booking_full_payment",
            Method = "momo_wallet",
            Amount = 1000m,
            Status = "success"
        };

        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking
            {
                BookingId = bookingId,
                TenantId = Guid.NewGuid(),
                PaymentMode = "full",
                UpfrontPaymentAmount = 1000m,
                DepositPaid = false,
                Status = "pending"
            }
        };

        var txService = new MomoTransactionServiceStub();
        txService.ByRequestId["REQ-IPN-2"] = new MomoTransaction
        {
            RequestId = "REQ-IPN-2",
            PaymentId = paymentId,
            Status = "pending",
            PartnerCode = "PARTNER",
            Amount = 1000,
            Type = "create_wallet_payment",
            RequestBody = "{}",
            ResponseBody = "{}",
            Message = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var controller = CreateMomoController(paymentService: paymentService, bookingService: bookingService, momoTransactionService: txService);

        var payload = BuildValidMomoIpnPayload(
            accessKey: "ACCESS",
            secretKey: "SECRET",
            requestId: "REQ-IPN-2",
            orderId: "ORD-IPN-2",
            resultCode: 0);
        SetRequestBody(controller, payload);

        var result = await controller.Ipn();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, bookingService.MarkDepositPaidCalls);
    }

    [Fact]
    public async Task MomoController_DisbursementIpn_ValidSignature_TriggersPayoutSync()
    {
        var payoutService = new LandlordPayoutServiceStub();
        var controller = CreateMomoController(
            momoService: new MomoServiceStub { ValidateDisbursementSignature = true },
            landlordPayoutService: payoutService);

        var payload = JsonSerializer.Serialize(new
        {
            requestId = "REQ-DISB-1",
            resultCode = 0,
            message = "OK",
            signature = "mocked-by-stub"
        });
        SetRequestBody(controller, payload);

        var result = await controller.DisbursementIpn();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, payoutService.SyncCalls);
    }

    private static LandlordController CreateLandlordController(
        ILandlordService? landlordService = null,
        ILandlordSubscriptionService? landlordSubscriptionService = null,
        IPaymentService? paymentService = null,
        IBookingService? bookingService = null,
        ILandlordPayoutService? landlordPayoutService = null)
    {
        return new LandlordController(
            landlordService ?? new LandlordServiceStub(),
            landlordSubscriptionService ?? new LandlordSubscriptionServiceStub(),
            paymentService ?? new PaymentServiceStub(),
            bookingService ?? new BookingServiceStub(),
            landlordPayoutService ?? new LandlordPayoutServiceStub(),
            CreateMapper());
    }

    private static MomoController CreateMomoController(
        IMomoService? momoService = null,
        IPaymentService? paymentService = null,
        IBookingService? bookingService = null,
        IMomoTransactionService? momoTransactionService = null,
        ILandlordSubscriptionService? landlordSubscriptionService = null,
        ILandlordService? landlordService = null,
        ILandlordPayoutService? landlordPayoutService = null)
    {
        return new MomoController(
            momoService ?? new MomoServiceStub(),
            paymentService ?? new PaymentServiceStub(),
            bookingService ?? new BookingServiceStub(),
            momoTransactionService ?? new MomoTransactionServiceStub(),
            landlordSubscriptionService ?? new LandlordSubscriptionServiceStub(),
            landlordService ?? new LandlordServiceStub(),
            landlordPayoutService ?? new LandlordPayoutServiceStub(),
            Options.Create(new MomoOptions
            {
                PartnerCode = "PARTNER",
                AccessKey = "ACCESS",
                SecretKey = "SECRET"
            }));
    }

    private static TenantController CreateTenantController(
        IBookingService? bookingService = null,
        IPaymentService? paymentService = null,
        IWishlistService? wishlistService = null)
    {
        return new TenantController(
            bookingService ?? new BookingServiceStub(),
            paymentService ?? new PaymentServiceStub(),
            wishlistService ?? new WishlistServiceStub(),
            CreateMapper());
    }

    private static IMapper CreateMapper()
    {
        return new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Payment, PaymentHistoryDto>();
        }, NullLoggerFactory.Instance).CreateMapper();
    }

    private static void SetUser(ControllerBase controller, params Claim[] claims)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static void SetRequestBody(ControllerBase controller, string body)
    {
        controller.ControllerContext ??= new ControllerContext();
        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    }

    private static T GetProperty<T>(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName) ?? throw new InvalidOperationException($"Property {propertyName} not found.");
        return (T)property.GetValue(value)!;
    }

    private static string BuildValidMomoIpnPayload(string accessKey, string secretKey, string requestId, string orderId, int resultCode)
    {
        var raw = string.Join("&", new[]
        {
            $"accessKey={accessKey}",
            "amount=1000",
            "message=Successful.",
            $"orderId={orderId}",
            "orderInfo=Booking deposit",
            "orderType=momo_wallet",
            "partnerCode=PARTNER",
            "payType=qr",
            $"requestId={requestId}",
            "responseTime=1710000000",
            $"resultCode={resultCode}",
            "transId=TRX-1"
        });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        return JsonSerializer.Serialize(new
        {
            amount = 1000,
            message = "Successful.",
            orderId,
            orderInfo = "Booking deposit",
            orderType = "momo_wallet",
            partnerCode = "PARTNER",
            payType = "qr",
            requestId,
            responseTime = 1710000000,
            resultCode,
            transId = "TRX-1",
            signature
        });
    }
}

internal abstract class BaseServiceStub<T> : IBaseService<T>
    where T : class
{
    public virtual Task<T?> GetByIdAsync(Guid id) => Task.FromResult<T?>(null);

    public virtual Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
        => Task.FromResult((Enumerable.Empty<T>(), 0));

    public virtual Task<T> CreateAsync(T entity) => Task.FromResult(entity);

    public virtual Task UpdateAsync(T entity) => Task.CompletedTask;

    public virtual Task DeleteAsync(Guid id) => Task.CompletedTask;
}

internal sealed class LandlordServiceStub : BaseServiceStub<Landlord>, ILandlordService
{
    public Landlord? LandlordByUserId { get; set; }

    public override Task<Landlord?> GetByIdAsync(Guid id)
        => Task.FromResult(LandlordByUserId != null && LandlordByUserId.LandlordId == id ? LandlordByUserId : null);

    public Task<Landlord?> GetByUserIdAsync(Guid userId) => Task.FromResult(LandlordByUserId);

    public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetOwnApartmentsAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null)
        => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

    public Task<SubscriptionPlanDto?> GetCurrentSubscriptionAsync(Guid landlordId) => Task.FromResult<SubscriptionPlanDto?>(null);

    public Task<LandlordPayoutProfileDto?> GetPayoutProfileAsync(Guid landlordId) => Task.FromResult<LandlordPayoutProfileDto?>(null);

    public Task<LandlordPayoutProfileDto> UpdateBankPayoutProfileAsync(Guid landlordId, UpdateBankPayoutProfileRequestDto request) => Task.FromResult(new LandlordPayoutProfileDto());

    public Task<LandlordPayoutProfileDto> UpdateMomoPayoutProfileAsync(Guid landlordId, UpdateMomoPayoutProfileRequestDto request) => Task.FromResult(new LandlordPayoutProfileDto());

    public Task<LandlordPayoutProfileDto> UpsertPayoutProfileAsync(Guid landlordId, UpsertLandlordPayoutProfileRequestDto request) => Task.FromResult(new LandlordPayoutProfileDto());
}

internal sealed class PaymentServiceStub : BaseServiceStub<Payment>, IPaymentService
{
    public List<Payment> LandlordPayments { get; set; } = new();
    public List<Payment> TenantPayments { get; set; } = new();
    public Dictionary<Guid, Payment> PaymentsById { get; } = new();

    public override Task<Payment?> GetByIdAsync(Guid id)
        => Task.FromResult(PaymentsById.TryGetValue(id, out var payment) ? payment : null);

    public Task<(IEnumerable<Payment> Items, int TotalCount)> GetLandlordPaymentsAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, DateTime? fromDate = null, DateTime? toDate = null)
        => Task.FromResult((LandlordPayments.AsEnumerable(), LandlordPayments.Count));

    public Task<(IEnumerable<Payment> Items, int TotalCount)> GetTenantPaymentsAsync(Guid tenantId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, DateTime? fromDate = null, DateTime? toDate = null)
        => Task.FromResult((TenantPayments.AsEnumerable(), TenantPayments.Count));
}

internal sealed class BookingServiceStub : BaseServiceStub<Booking>, IBookingService
{
    public Booking? BookingById { get; set; }
    public int MarkDepositPaidCalls { get; private set; }
    public int MarkBalancePaidCalls { get; private set; }

    public override Task<Booking?> GetByIdAsync(Guid id)
        => Task.FromResult(BookingById != null && BookingById.BookingId == id ? BookingById : null);

    public Task<BookingQuoteResponseDto> GetQuoteAsync(BookingQuoteRequestDto dto) => Task.FromResult(new BookingQuoteResponseDto());
    public Task<Booking> CreateWithQuoteAsync(CreateBookingRequestDto requestDto, Guid tenantId) => Task.FromResult(new Booking());
    public Task<Booking> MarkDepositPaidAsync(Guid bookingId)
    {
        MarkDepositPaidCalls++;
        return Task.FromResult(BookingById ?? new Booking { BookingId = bookingId });
    }

    public Task<Booking> MarkBalancePaidAsync(Guid bookingId)
    {
        MarkBalancePaidCalls++;
        return Task.FromResult(BookingById ?? new Booking { BookingId = bookingId });
    }
    public Task<TemporaryResidenceReport> SubmitResidenceReportAsync(Guid bookingId, Guid landlordUserId, SubmitResidenceReportDto dto) => Task.FromResult(new TemporaryResidenceReport());
    public Task<TemporaryResidenceReportDetailsDto> GetResidenceReportDetailsAsync(Guid bookingId, Guid requesterUserId) => Task.FromResult(new TemporaryResidenceReportDetailsDto());
    public Task<(IEnumerable<Booking> Items, int TotalCount)> GetLandlordBookingHistoryAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null)
        => Task.FromResult((Enumerable.Empty<Booking>(), 0));
    public Task<BookingCheckTimeResponseDto> RecordCheckInAsync(Guid bookingId, RecordCheckInDto dto, Guid recordedBy) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> RecordCheckOutAsync(Guid bookingId, RecordCheckOutDto dto, Guid recordedBy) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> GetCheckTimeDetailsAsync(Guid bookingId, Guid? requesterId = null) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> RespondToCheckTimeAsync(Guid bookingId, Guid tenantId, RespondBookingCheckTimeDto dto) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> ResolveCheckTimeDisputeAsync(Guid bookingId, Guid resolvedBy, ResolveBookingCheckTimeDisputeDto dto) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<AvailabilityCalendarResponseDto> GetAvailabilityCalendarAsync(Guid apartmentId, DateTime? startDate = null, DateTime? endDate = null, Guid? requesterId = null, string? requesterRole = null) => Task.FromResult(new AvailabilityCalendarResponseDto());
    public Task<SetApartmentAvailabilityResponseDto> SetApartmentAvailabilityAsync(Guid apartmentId, Guid landlordId, SetApartmentAvailabilityRequestDto dto) => Task.FromResult(new SetApartmentAvailabilityResponseDto());
    public Task<RemoveApartmentAvailabilityResponseDto> RemoveApartmentAvailabilityAsync(Guid apartmentId, Guid landlordId, RemoveApartmentAvailabilityRequestDto dto) => Task.FromResult(new RemoveApartmentAvailabilityResponseDto());
    public Task<IReadOnlyList<OccupiedRoomAlternativeOptionDto>> FindAlternativeApartmentsAsync(Guid bookingId, int maxResults = 5) => Task.FromResult<IReadOnlyList<OccupiedRoomAlternativeOptionDto>>(Array.Empty<OccupiedRoomAlternativeOptionDto>());
    public Task<BookingOfferResponseDto> CreateAlternativeOfferAsync(Guid bookingId, Guid alternativeApartmentId, Guid? staffUserId, string? reason = null, int? expiresInHours = null) => Task.FromResult(new BookingOfferResponseDto());
    public Task<IReadOnlyList<BookingOfferResponseDto>> GetTenantActiveOffersAsync(Guid tenantId) => Task.FromResult<IReadOnlyList<BookingOfferResponseDto>>(Array.Empty<BookingOfferResponseDto>());
    public Task<BookingOfferResponseDto> RespondToAlternativeOfferAsync(Guid offerId, Guid tenantId, bool accepted, string? notes = null) => Task.FromResult(new BookingOfferResponseDto());
    public Task<ConfirmOccupiedIncidentPenaltyResponseDto> ConfirmOccupiedIncidentPenaltyAsync(Guid bookingId, Guid confirmedBy, Guid? ticketId = null, string? notes = null) => Task.FromResult(new ConfirmOccupiedIncidentPenaltyResponseDto());
}

internal sealed class MomoTransactionServiceStub : BaseServiceStub<MomoTransaction>, IMomoTransactionService
{
    public readonly List<MomoTransaction> Created = new();
    public readonly List<MomoTransaction> Updated = new();
    public readonly Dictionary<string, MomoTransaction> ByRequestId = new(StringComparer.Ordinal);
    public readonly Dictionary<string, MomoTransaction> ByBodyContains = new(StringComparer.Ordinal);

    public Task<MomoTransaction?> FindByRequestIdAsync(string requestId)
    {
        return Task.FromResult(ByRequestId.TryGetValue(requestId, out var item) ? item : null);
    }

    public Task<MomoTransaction?> FindByRequestBodyContainsAsync(string content)
    {
        if (ByBodyContains.TryGetValue(content, out var item))
        {
            return Task.FromResult<MomoTransaction?>(item);
        }

        var found = ByRequestId.Values.FirstOrDefault(x => (x.RequestBody ?? string.Empty).Contains(content, StringComparison.Ordinal));
        return Task.FromResult(found);
    }

    public override Task<MomoTransaction> CreateAsync(MomoTransaction entity)
    {
        Created.Add(entity);
        return Task.FromResult(entity);
    }

    public override Task UpdateAsync(MomoTransaction entity)
    {
        Updated.Add(entity);
        return Task.CompletedTask;
    }
}

internal sealed class MomoServiceStub : IMomoService
{
    public bool ValidateDisbursementSignature { get; set; } = true;

    public Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MomoCreatePaymentResponse());

    public Task<MomoDisbursementResponse> VerifyWalletAsync(MomoVerifyWalletRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MomoDisbursementResponse());

    public Task<MomoDisbursementResponse> CreateDisbursementAsync(MomoDisbursementRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MomoDisbursementResponse());

    public Task<MomoQueryDisbursementResponse> QueryDisbursementStatusAsync(MomoQueryDisbursementRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MomoQueryDisbursementResponse());

    public bool ValidateDisbursementIpnSignature(string requestBody) => ValidateDisbursementSignature;
}

internal sealed class LandlordSubscriptionServiceStub : BaseServiceStub<LandlordSubscription>, ILandlordSubscriptionService
{
    public Task<(IEnumerable<LandlordSubscription> Items, int TotalCount)> GetHistoryForLandlordAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null)
        => Task.FromResult((Enumerable.Empty<LandlordSubscription>(), 0));

    public Task<MomoCreatePaymentResponse> CreateMomoSubscriptionCheckoutAsync(Guid landlordId, StartLandlordSubscriptionRequestDto dto, CancellationToken cancellationToken = default)
        => Task.FromResult(new MomoCreatePaymentResponse());

    public Task<WalletSubscriptionPaymentResponseDto> PaySubscriptionByWalletAsync(Guid landlordId, StartLandlordSubscriptionRequestDto dto, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletSubscriptionPaymentResponseDto());
}

internal sealed class LandlordPayoutServiceStub : ILandlordPayoutService
{
    public int SyncCalls { get; private set; }

    public Task<LandlordPayoutResponseDto> CreatePayoutAsync(Guid landlordId, CreateLandlordPayoutRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new LandlordPayoutResponseDto());

    public Task<(IEnumerable<LandlordPayoutResponseDto> Items, int TotalCount)> GetPayoutHistoryAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null)
        => Task.FromResult((Enumerable.Empty<LandlordPayoutResponseDto>(), 0));

    public Task<LandlordPayoutResponseDto?> GetPayoutByIdAsync(Guid landlordId, Guid payoutId)
        => Task.FromResult<LandlordPayoutResponseDto?>(null);

    public Task<int> SyncProcessingPayoutsAsync(CancellationToken cancellationToken = default)
    {
        SyncCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class WishlistServiceStub : BaseServiceStub<TenantWishlist>, IWishlistService
{
    public Task<(IEnumerable<WishlistItemResponseDto> Items, int TotalCount)> GetTenantWishlistAsync(Guid tenantId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, decimal? priceMin = null, decimal? priceMax = null, Guid? collectionId = null, Dictionary<string, string>? filters = null)
        => Task.FromResult((Enumerable.Empty<WishlistItemResponseDto>(), 0));

    public Task<WishlistItemResponseDto> AddToWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null, string? notes = null)
        => Task.FromResult(new WishlistItemResponseDto());

    public Task RemoveFromWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null)
        => Task.CompletedTask;

    public Task<WishlistItemResponseDto> ToggleFavoriteAsync(Guid tenantId, Guid apartmentId, bool isFavorite, Guid? collectionId = null)
        => Task.FromResult(new WishlistItemResponseDto());

    public Task<int> GetWishlistCountAsync(Guid tenantId) => Task.FromResult(0);

    public Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid? collectionId = null) => Task.FromResult(false);

    public Task<WishlistCollectionResponseDto> CreateCollectionAsync(Guid tenantId, string name, string? description = null)
        => Task.FromResult(new WishlistCollectionResponseDto());

    public Task<(IEnumerable<WishlistCollectionResponseDto> Items, int TotalCount)> GetCollectionsAsync(Guid tenantId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null)
        => Task.FromResult((Enumerable.Empty<WishlistCollectionResponseDto>(), 0));

    public Task<WishlistCollectionResponseDto> UpdateCollectionAsync(Guid tenantId, Guid collectionId, string name, string? description = null)
        => Task.FromResult(new WishlistCollectionResponseDto());

    public Task DeleteCollectionAsync(Guid tenantId, Guid collectionId)
        => Task.CompletedTask;

    public Task<WishlistItemResponseDto> MoveWishlistItemAsync(Guid tenantId, Guid apartmentId, Guid sourceCollectionId, Guid targetCollectionId)
        => Task.FromResult(new WishlistItemResponseDto());
}