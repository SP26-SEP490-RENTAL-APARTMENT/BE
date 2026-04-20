using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
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
using Short_termApartmentAPI.Services;

#pragma warning disable CS8602

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

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var items = GetProperty<IEnumerable<PaymentHistoryDto>>(ok.Value!, "Items");
        var totalCount = GetProperty<int>(ok.Value!, "TotalCount");

        Assert.Equal(1, totalCount);
        var dto = Assert.Single(items);
        Assert.Equal(125000m, dto.Amount);
        Assert.Equal("success", dto.Status);
        Assert.Equal("txn-1", dto.TransactionId);
    }

    [Fact]
    public async Task LandlordController_GetWallet_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var controller = CreateLandlordController();
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        var result = await controller.GetWallet();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task LandlordController_GetWallet_ReturnsBalances()
    {
        var landlordId = Guid.NewGuid();
        var walletService = new LandlordWalletServiceStub
        {
            Wallet = new LandlordWallet
            {
                LandlordId = landlordId,
                PendingBalance = 50000m,
                AvailableBalance = 120000m,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var controller = CreateLandlordController(
            landlordService: new LandlordServiceStub
            {
                LandlordByUserId = new Landlord { LandlordId = landlordId, LandlordNavigation = new User { UserId = landlordId } }
            },
            landlordWalletService: walletService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, landlordId.ToString()));

        var result = await controller.GetWallet();

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var response = ok.Value as Short_termApartmentAPI.Middlewares.ApiResponse<LandlordWalletBalanceDto>
            ?? throw new InvalidOperationException("Expected wallet response.");

        Assert.Equal(50000m, response.Data!.PendingBalance);
        Assert.Equal(120000m, response.Data.AvailableBalance);
        Assert.Equal(170000m, response.Data.TotalBalance);
    }

    [Fact]
    public async Task MomoController_Ipn_ReturnsOk_WhenSignatureIsInvalid()
    {
        var controller = CreateMomoController(momoService: new MomoServiceStub { ValidateDisbursementSignature = false });
        SetRequestBody(controller, "{\"signature\":\"bad\"}");

        var result = await controller.Ipn();

        Assert.IsType<OkObjectResult>(result);
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

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var response = ok.Value as Short_termApartmentAPI.Middlewares.ApiResponse<PaymentHistoryDto>
            ?? throw new InvalidOperationException("Expected payment response.");
        var data = response.Data!;
        Assert.Equal(777m, data.Amount);
        Assert.Equal("txn-tenant-1", data.TransactionId);
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

        Assert.True(result is OkObjectResult);
        Assert.Equal(1, payoutService.SyncCalls);
    }

    [Fact]
    public async Task BookingController_GetOccupants_ReturnsMappedOccupants()
    {
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking { BookingId = bookingId, TenantId = tenantId }
        };
        bookingService.OccupantsByBooking[bookingId] = new List<ResidenceReportOccupantDto>
        {
            new ResidenceReportOccupantDto { Order = 1, FullName = "Tenant One", PassportId = "P1" },
            new ResidenceReportOccupantDto { Order = 2, FullName = "Tenant Two", PassportId = "P2" }
        };

        var controller = CreateBookingController(bookingService: bookingService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()), new Claim(ClaimTypes.Role, "tenant"));

        var result = await controller.GetOccupants(bookingId);

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var response = ok.Value as Short_termApartmentAPI.Middlewares.ApiResponse<IReadOnlyList<ResidenceReportOccupantDto>>
            ?? throw new InvalidOperationException("Expected occupants response.");
        var data = response.Data!;
        Assert.Equal(2, data.Count);
        Assert.Equal("Tenant One", data.First().FullName);
        Assert.Equal("Tenant Two", data.Last().FullName);
    }

    [Fact]
    public async Task BookingController_AddOccupant_AddsOccupantAndReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking { BookingId = bookingId, TenantId = tenantId }
        };

        var controller = CreateBookingController(bookingService: bookingService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()), new Claim(ClaimTypes.Role, "tenant"));

        var photoStream = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-content"));
        var proofPhoto = new FormFile(photoStream, 0, photoStream.Length, "ProofPhoto", "proof.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.AddOccupant(bookingId, new AddBookingOccupantFormDto
        {
            FullName = "New Occupant",
            PassportId = "P-NEW",
            DateOfBirth = new DateOnly(1997, 5, 10),
            NationalIdCardNumber = "123456789012",
            Nationality = "VN",
            Sex = "female",
            ProofPhoto = proofPhoto
        });

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var response = ok.Value as Short_termApartmentAPI.Middlewares.ApiResponse<ResidenceReportOccupantDto>
            ?? throw new InvalidOperationException("Expected occupant response.");
    var data = response.Data!;
        Assert.Equal(1, data.Order);
        Assert.Equal("New Occupant", data.FullName);
        Assert.Equal(new DateOnly(1997, 5, 10), data.DateOfBirth);
        Assert.Single(bookingService.OccupantsByBooking[bookingId]);
    }

    [Fact]
    public async Task BookingController_UpdateOccupant_UpdatesOccupantAndReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking { BookingId = bookingId, TenantId = tenantId }
        };
        bookingService.OccupantsByBooking[bookingId] = new List<ResidenceReportOccupantDto>
        {
            new ResidenceReportOccupantDto { Order = 1, FullName = "Old Name", PassportId = "OLD" }
        };

        var controller = CreateBookingController(bookingService: bookingService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()), new Claim(ClaimTypes.Role, "tenant"));

        var photoStream = new MemoryStream(Encoding.UTF8.GetBytes("replacement-image-content"));
        var proofPhoto = new FormFile(photoStream, 0, photoStream.Length, "ProofPhoto", "replacement.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.UpdateOccupant(bookingId, 1, new UpdateBookingOccupantFormDto
        {
            FullName = "Updated Name",
            PassportId = "NEW",
            DateOfBirth = new DateOnly(1992, 1, 2),
            IsPrimary = true,
            ProofPhoto = proofPhoto
        });

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var response = ok.Value as Short_termApartmentAPI.Middlewares.ApiResponse<ResidenceReportOccupantDto>
            ?? throw new InvalidOperationException("Expected occupant response.");
    var data = response.Data!;
        Assert.Equal("Updated Name", data.FullName);
        Assert.True(data.IsPrimary);
        Assert.Equal("NEW", data.PassportId);
        Assert.Equal(new DateOnly(1992, 1, 2), data.DateOfBirth);
        Assert.Equal("https://example.test/uploads/replacement.jpg", data.ProofPhotoUrl);
    }

    [Fact]
    public async Task BookingController_RemoveOccupant_RemovesOccupantAndReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bookingService = new BookingServiceStub
        {
            BookingById = new Booking { BookingId = bookingId, TenantId = tenantId }
        };
        bookingService.OccupantsByBooking[bookingId] = new List<ResidenceReportOccupantDto>
        {
            new ResidenceReportOccupantDto { Order = 1, FullName = "Primary" },
            new ResidenceReportOccupantDto { Order = 2, FullName = "Secondary" }
        };

        var controller = CreateBookingController(bookingService: bookingService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, tenantId.ToString()), new Claim(ClaimTypes.Role, "tenant"));

        var result = await controller.RemoveOccupant(bookingId, 2);

        var ok = result as OkObjectResult ?? throw new InvalidOperationException("Expected OK result.");
        var response = ok.Value as Short_termApartmentAPI.Middlewares.ApiResponse<string>
            ?? throw new InvalidOperationException("Expected success response.");
        Assert.Equal("Occupant removed successfully.", response.Message);
        Assert.Single(bookingService.OccupantsByBooking[bookingId]);
        Assert.Equal(1, bookingService.OccupantsByBooking[bookingId][0].Order);
    }

    private static LandlordController CreateLandlordController(
        ILandlordService? landlordService = null,
        ILandlordSubscriptionService? landlordSubscriptionService = null,
        IPaymentService? paymentService = null,
        IBookingService? bookingService = null,
        ILandlordWalletService? landlordWalletService = null,
        ILandlordPayoutService? landlordPayoutService = null)
    {
        return new LandlordController(
            landlordService ?? new LandlordServiceStub(),
            landlordSubscriptionService ?? new LandlordSubscriptionServiceStub(),
            paymentService ?? new PaymentServiceStub(),
            bookingService ?? new BookingServiceStub(),
            landlordWalletService ?? new LandlordWalletServiceStub(),
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
            new MomoWebhookService(
                NullLogger<MomoWebhookService>.Instance,
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
                })),
            NullLogger<MomoController>.Instance);
    }

    private static BookingController CreateBookingController(
        IBookingService? bookingService = null,
        IPaymentService? paymentService = null,
        ISupportTicketService? supportTicketService = null,
        IStripeService? stripeService = null,
        IMomoService? momoService = null,
        IMomoTransactionService? momoTransactionService = null,
        IImageService? imageService = null,
        IResidenceReportPdfGenerator? residenceReportPdfGenerator = null,
        IResidenceReportDocxGenerator? residenceReportDocxGenerator = null)
    {
        return new BookingController(
            bookingService ?? new BookingServiceStub(),
            paymentService ?? new PaymentServiceStub(),
            supportTicketService ?? new SupportTicketServiceStub(),
            stripeService ?? new StripeServiceStub(),
            momoService ?? new MomoServiceStub(),
            momoTransactionService ?? new MomoTransactionServiceStub(),
            imageService ?? new ImageServiceStub(),
            Options.Create(new MomoOptions
            {
                PartnerCode = "PARTNER",
                AccessKey = "ACCESS",
                SecretKey = "SECRET"
            }),
            residenceReportPdfGenerator ?? new ResidenceReportPdfGeneratorStub(),
            residenceReportDocxGenerator ?? new ResidenceReportDocxGeneratorStub(),
            CreateMapper());
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
        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 1000,
            ["extraData"] = string.Empty,
            ["message"] = "Successful.",
            ["orderId"] = orderId,
            ["orderInfo"] = "Booking deposit",
            ["orderType"] = "momo_wallet",
            ["partnerCode"] = "PARTNER",
            ["payType"] = "qr",
            ["requestId"] = requestId,
            ["responseTime"] = 1710000000,
            ["resultCode"] = resultCode,
            ["transId"] = "TRX-1"
        };

        var raw = BuildCanonicalIpnRaw(accessKey, payload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        payload["signature"] = signature;

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildCanonicalIpnRaw(string accessKey, IReadOnlyDictionary<string, object?> payload)
    {
        static string GetValueOrEmpty(IReadOnlyDictionary<string, object?> dictionary, string key)
        {
            if (!dictionary.TryGetValue(key, out var value) || value is null)
                return string.Empty;

            return value switch
            {
                string s => s,
                bool b => b ? "true" : "false",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        return string.Join("&", new[]
        {
            $"accessKey={accessKey}",
            $"amount={GetValueOrEmpty(payload, "amount")}",
            $"extraData={GetValueOrEmpty(payload, "extraData")}",
            $"message={GetValueOrEmpty(payload, "message")}",
            $"orderId={GetValueOrEmpty(payload, "orderId")}",
            $"orderInfo={GetValueOrEmpty(payload, "orderInfo")}",
            $"orderType={GetValueOrEmpty(payload, "orderType")}",
            $"partnerCode={GetValueOrEmpty(payload, "partnerCode")}",
            $"payType={GetValueOrEmpty(payload, "payType")}",
            $"requestId={GetValueOrEmpty(payload, "requestId")}",
            $"responseTime={GetValueOrEmpty(payload, "responseTime")}",
            $"resultCode={GetValueOrEmpty(payload, "resultCode")}",
            $"transId={GetValueOrEmpty(payload, "transId")}",
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
    public Dictionary<Guid, List<ResidenceReportOccupantDto>> OccupantsByBooking { get; } = new();

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
    public Task<IReadOnlyList<ResidenceReportOccupantDto>> GetOccupantsAsync(Guid bookingId, Guid tenantUserId)
        => Task.FromResult<IReadOnlyList<ResidenceReportOccupantDto>>(GetOccupantsInternal(bookingId));

    public Task<ResidenceReportOccupantDto> AddOccupantAsync(Guid bookingId, Guid tenantUserId, AddBookingOccupantDto dto)
    {
        var occupants = GetOccupantsInternal(bookingId).ToList();
        var nextOrder = occupants.Count == 0 ? 1 : occupants.Max(x => x.Order) + 1;
        var occupant = new ResidenceReportOccupantDto
        {
            Order = nextOrder,
            IsPrimary = occupants.Count == 0,
            FullName = dto.FullName,
            PassportId = dto.PassportId,
            DateOfBirth = dto.DateOfBirth,
            NationalIdCardNumber = dto.NationalIdCardNumber,
            Nationality = dto.Nationality,
            Sex = dto.Sex,
            Phone = dto.Phone,
            Email = dto.Email,
            ProofPhotoUrl = dto.ProofPhotoUrl
        };

        occupants.Add(occupant);
        OccupantsByBooking[bookingId] = occupants.OrderBy(x => x.Order).ToList();
        return Task.FromResult(occupant);
    }

    public Task<ResidenceReportOccupantDto> UpdateOccupantAsync(Guid bookingId, Guid tenantUserId, int occupantOrder, UpdateBookingOccupantDto dto)
    {
        var occupants = GetOccupantsInternal(bookingId).ToList();
        var occupant = occupants.FirstOrDefault(x => x.Order == occupantOrder) ?? new ResidenceReportOccupantDto { Order = occupantOrder };

        occupant.IsPrimary = dto.IsPrimary ?? occupant.IsPrimary;
        occupant.FullName = dto.FullName ?? occupant.FullName;
        occupant.PassportId = dto.PassportId ?? occupant.PassportId;
        occupant.DateOfBirth = dto.DateOfBirth ?? occupant.DateOfBirth;
        occupant.NationalIdCardNumber = dto.NationalIdCardNumber ?? occupant.NationalIdCardNumber;
        occupant.Nationality = dto.Nationality ?? occupant.Nationality;
        occupant.Sex = dto.Sex ?? occupant.Sex;
        occupant.Phone = dto.Phone ?? occupant.Phone;
        occupant.Email = dto.Email ?? occupant.Email;
        occupant.ProofPhotoUrl = dto.ProofPhotoUrl ?? occupant.ProofPhotoUrl;

        occupants.RemoveAll(x => x.Order == occupantOrder);
        occupants.Add(occupant);
        OccupantsByBooking[bookingId] = occupants.OrderBy(x => x.Order).ToList();
        return Task.FromResult(occupant);
    }

    public Task RemoveOccupantAsync(Guid bookingId, Guid tenantUserId, int occupantOrder)
    {
        var occupants = GetOccupantsInternal(bookingId).ToList();
        occupants.RemoveAll(x => x.Order == occupantOrder);
        OccupantsByBooking[bookingId] = occupants.OrderBy(x => x.Order).ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ResidenceReportOccupantDto>> FillOccupantsManuallyAsync(Guid bookingId, Guid tenantUserId, FillBookingOccupantsDto dto)
    {
        var occupants = dto.Occupants
            .OrderBy(x => x.OccupantOrder)
            .Select(x => new ResidenceReportOccupantDto
            {
                Order = x.OccupantOrder,
                IsPrimary = x.IsPrimary,
                FullName = x.FullName,
                PassportId = x.PassportId,
                DateOfBirth = x.DateOfBirth,
                NationalIdCardNumber = x.NationalIdCardNumber,
                Nationality = x.Nationality,
                Sex = x.Sex,
                Phone = x.Phone,
                Email = x.Email,
                ProofPhotoUrl = x.ProofPhotoUrl
            })
            .ToList();

        if (!occupants.Any(x => x.IsPrimary) && occupants.Count > 0)
        {
            occupants[0].IsPrimary = true;
        }

        OccupantsByBooking[bookingId] = occupants;
        return Task.FromResult<IReadOnlyList<ResidenceReportOccupantDto>>(occupants);
    }

    private List<ResidenceReportOccupantDto> GetOccupantsInternal(Guid bookingId)
    {
        return OccupantsByBooking.TryGetValue(bookingId, out var occupants)
            ? occupants.ToList()
            : new List<ResidenceReportOccupantDto>();
    }
    public Task<(IEnumerable<Booking> Items, int TotalCount)> GetLandlordBookingHistoryAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null)
        => Task.FromResult((Enumerable.Empty<Booking>(), 0));
    public Task<BookingCheckTimeResponseDto> RecordCheckInAsync(Guid bookingId, RecordCheckInDto dto, Guid recordedBy) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> RecordCheckOutAsync(Guid bookingId, RecordCheckOutDto dto, Guid recordedBy) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> GetCheckTimeDetailsAsync(Guid bookingId, Guid? requesterId = null) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> RespondToCheckTimeAsync(Guid bookingId, Guid tenantId, RespondBookingCheckTimeDto dto) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> ResolveCheckTimeDisputeAsync(Guid bookingId, Guid resolvedBy, ResolveBookingCheckTimeDisputeDto dto) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> SettleCheckTimeFeeAsync(Guid bookingId, Guid settledBy, SettleBookingCheckTimeFeeDto dto) => Task.FromResult(new BookingCheckTimeResponseDto());
    public Task<BookingCheckTimeResponseDto> SubmitPaymentConfirmationAsync(Guid bookingId, Guid landlordId, LandlordPaymentConfirmationDto dto) => Task.FromResult(new BookingCheckTimeResponseDto());
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

    public Task<IReadOnlyList<MomoTransaction>> GetPendingIpnQueueItemsAsync(int take, DateTime retryReadyAtOrBefore)
    {
        var items = ByRequestId.Values
            .Where(i => i.Type == "ipn_queue"
                && (i.Status == "queued" || (i.Status == "retry_wait" && i.UpdatedAt <= retryReadyAtOrBefore)))
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<MomoTransaction>>(items);
    }

    public Task<IReadOnlyList<MomoTransaction>> GetPendingWalletPaymentRequestsAsync(int take, DateTime createdBefore)
    {
        var items = ByRequestId.Values
            .Where(i => (i.Type == "create_wallet_payment" || i.Type == "create_wallet_payment_subscription")
                && i.Status == "pending"
                && i.CreatedAt <= createdBefore)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<MomoTransaction>>(items);
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

internal sealed class ImageServiceStub : IImageService
{
    public Task<string> UploadImageAsync(IFormFile file)
    {
        var fileName = string.IsNullOrWhiteSpace(file?.FileName) ? "proof.jpg" : file.FileName;
        return Task.FromResult($"https://example.test/uploads/{fileName}");
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

    public Task<MomoQueryPaymentResponse> QueryPaymentStatusAsync(MomoQueryPaymentRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MomoQueryPaymentResponse());

    public bool ValidateDisbursementIpnSignature(string requestBody) => ValidateDisbursementSignature;
}

internal sealed class SupportTicketServiceStub : BaseServiceStub<SupportTicket>, ISupportTicketService
{
    public Task<SupportTicket> CreateTicketAsync(SupportTicket ticket) => Task.FromResult(ticket);

    public Task<SupportTicket> UpdateTicketByStaffAsync(Guid ticketId, UpdateSupportTicketDto ticketDto, Guid actorUserId)
        => Task.FromResult(new SupportTicket());

    public Task<SupportTicket> CreateFollowUpTicketAsync(Guid originalTicketId, Guid requesterUserId, string details)
        => Task.FromResult(new SupportTicket());
}

internal sealed class StripeServiceStub : IStripeService
{
    public Task<StripeCheckoutResponseDto> CreateCheckoutSessionAsync(StripeCheckoutRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new StripeCheckoutResponseDto());
}

internal sealed class ResidenceReportPdfGeneratorStub : IResidenceReportPdfGenerator
{
    public Task<byte[]> GenerateAsync(TemporaryResidenceReportDetailsDto details, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());
}

internal sealed class ResidenceReportDocxGeneratorStub : IResidenceReportDocxGenerator
{
    public Task<byte[]> GenerateAsync(TemporaryResidenceReportDetailsDto details, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());
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

internal sealed class LandlordWalletServiceStub : ILandlordWalletService
{
    public LandlordWallet Wallet { get; set; } = new()
    {
        LandlordId = Guid.NewGuid(),
        PendingBalance = 0m,
        AvailableBalance = 0m,
        UpdatedAt = DateTime.UtcNow
    };

    public Task<LandlordWallet> GetOrCreateAsync(Guid landlordId)
    {
        Wallet.LandlordId = landlordId;
        return Task.FromResult(Wallet);
    }

    public Task CreditPendingAsync(Guid landlordId, decimal amount) => Task.CompletedTask;

    public Task DebitAvailableAsync(Guid landlordId, decimal amount) => Task.CompletedTask;

    public Task<LandlordPenaltyApplicationResultDto> ApplyOccupiedIncidentPenaltyAsync(Guid landlordId, decimal amount)
        => Task.FromResult(new LandlordPenaltyApplicationResultDto());

    public Task ReserveForPayoutAsync(Guid landlordId, long amount) => Task.CompletedTask;

    public Task FinalizePayoutSuccessAsync(Guid landlordId, long amount) => Task.CompletedTask;

    public Task RollbackPayoutAsync(Guid landlordId, long amount) => Task.CompletedTask;
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

#pragma warning restore CS8602