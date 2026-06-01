using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/landlord")]
[Authorize(Roles = "landlord")]
public sealed class LandlordController : ControllerBase
{
    private readonly ILandlordService _landlordService;
    private readonly ILandlordSubscriptionService _landlordSubscriptionService;
    private readonly IPaymentService _paymentService;
    private readonly IBookingService _bookingService;
    private readonly ILandlordWalletService _landlordWalletService;
    private readonly ILandlordPayoutService _landlordPayoutService;
    private readonly IMapper _mapper;
    private readonly PayOSClient _payOsClient;
    private readonly StripeSettings _stripeSettings;

    public LandlordController(
        ILandlordService landlordService,
        ILandlordSubscriptionService landlordSubscriptionService,
        IPaymentService paymentService,
        IBookingService bookingService,
        ILandlordWalletService landlordWalletService,
        ILandlordPayoutService landlordPayoutService,
        IMapper mapper,
        [FromKeyedServices("OrderClient")] PayOSClient payOsClient,
        IOptions<StripeSettings> stripeSettings)
    {
        _landlordService = landlordService;
        _landlordSubscriptionService = landlordSubscriptionService;
        _paymentService = paymentService;
        _bookingService = bookingService;
        _landlordWalletService = landlordWalletService;
        _landlordPayoutService = landlordPayoutService;
        _mapper = mapper;
        _payOsClient = payOsClient;
        _stripeSettings = stripeSettings.Value;
    }
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "landlord ok" });

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        var (_, propertyCount) = await _landlordService.GetOwnApartmentsAsync(landlord.LandlordId, 1, 1);
        var (_, bookingCount) = await _bookingService.GetLandlordBookingHistoryAsync(landlord.LandlordId, 1, 1);
        var revenue = await _paymentService.GetLandlordRevenueTotalAsync(landlord.LandlordId);

        return Ok(new ApiResponse<LandlordDashboardSummaryDto>(new LandlordDashboardSummaryDto
        {
            GeneratedAt = Common.Utils.VietnamTime.Now,
            TotalProperties = propertyCount,
            TotalBookings = bookingCount,
            TotalRevenue = revenue,
        }));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        return Ok(new ApiResponse<Landlord>(landlord));
    }

    [HttpGet("apartments")]
    public async Task<IActionResult> GetOwnApartments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var (items, totalCount) = await _landlordService.GetOwnApartmentsAsync(landlord.LandlordId, page, pageSize, sortBy, sortOrder, search, filters);
        var mapped = _mapper.Map<IEnumerable<ApartmentResponseDto>>(items);
        return Ok(new { Items = mapped, TotalCount = totalCount });
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var plan = await _landlordService.GetCurrentSubscriptionAsync(landlord.LandlordId);
        if (plan == null)
            return NotFound(new ApiResponse<string>("No active subscription."));

        return Ok(new ApiResponse<SubscriptionPlanDto>(plan));
    }

    [HttpGet("subscriptions/history")]
    public async Task<IActionResult> GetSubscriptionHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var (items, totalCount) = await _landlordSubscriptionService.GetHistoryForLandlordAsync(
            landlord.LandlordId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            fromDate,
            toDate);

        var dtos = _mapper.Map<IEnumerable<LandlordSubscriptionHistoryDto>>(items);
        return Ok(new { Items = dtos, TotalCount = totalCount });
    }

    [HttpGet("payments/history")]
    public async Task<IActionResult> GetPaymentHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var (items, totalCount) = await _paymentService.GetLandlordPaymentsAsync(
            landlord.LandlordId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            fromDate,
            toDate,
            filters);

        var dtos = _mapper.Map<IEnumerable<PaymentHistoryDto>>(items);
        ApplySignedAmountDisplay(dtos, isTenantView: false);
        return Ok(new { Items = dtos, TotalCount = totalCount });
    }

    private static void ApplySignedAmountDisplay(IEnumerable<PaymentHistoryDto> payments, bool isTenantView)
    {
        foreach (var payment in payments)
        {
            var isRefund = string.Equals(payment.PaymentType, "refund", StringComparison.OrdinalIgnoreCase);
            var shouldShowPositive = isTenantView ? isRefund : !isRefund;
            var sign = shouldShowPositive ? "+" : "-";
            payment.SignedAmountDisplay = $"{sign}{payment.Amount:N0}";
        }
    }

    [HttpGet("bookings/history")]
    public async Task<IActionResult> GetBookingHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var (items, totalCount) = await _bookingService.GetLandlordBookingHistoryAsync(
            landlord.LandlordId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            fromDate,
            toDate);

        var dtos = _mapper.Map<IEnumerable<BookingResponseDto>>(items);
        return Ok(new { Items = dtos, TotalCount = totalCount });
    }

    [HttpPost("subscription/momo-checkout")]
    public async Task<IActionResult> CreateSubscriptionMomoCheckout(
        [FromBody] StartLandlordSubscriptionRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }
        try
        {
            var momoResult = await _landlordSubscriptionService.CreateMomoSubscriptionCheckoutAsync(
                landlord.LandlordId,
                dto,
                cancellationToken);

            return Ok(new ApiResponse<MomoCreatePaymentResponse>(momoResult));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPost("subscription/payos-checkout")]
    public async Task<IActionResult> CreateSubscriptionPayOsCheckout(
        [FromBody] StartLandlordSubscriptionRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        try
        {
            var (plan, _, amount) = await _landlordSubscriptionService.ResolvePlanAndAmountAsync(dto);

            var isAndroid = string.Equals(dto.DevicePlatform?.Trim(), "android", StringComparison.OrdinalIgnoreCase);
            var payosRequest = new CreatePaymentLinkRequest
            {
                OrderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Amount = (long)Math.Round(amount),
                Description = "Subscription payment",
                ReturnUrl = string.IsNullOrWhiteSpace(dto.ReturnUrl)
                    ? isAndroid ? "VStay://payos-payment" : _stripeSettings.SuccessUrl
                    : dto.ReturnUrl,
                CancelUrl = string.IsNullOrWhiteSpace(dto.CancelUrl)
                    ? isAndroid ? "VStay://payos-payment" : _stripeSettings.CancelUrl
                    : dto.CancelUrl,
                Items = [new PaymentLinkItem { Name = plan.Name, Quantity = 1, Price = (long)Math.Round(amount), Unit = "subscription" }]
            };

            CreatePaymentLinkResponse payosResponse;
            try
            {
                payosResponse = await _payOsClient.PaymentRequests.CreateAsync(payosRequest);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    new ApiResponse<string>($"PayOS payment link creation failed: {ex.Message}"));
            }

            var result = await _landlordSubscriptionService.CreatePayOsSubscriptionCheckoutAsync(
                landlord.LandlordId,
                dto,
                payosRequest,
                payosResponse,
                cancellationToken);

            return Ok(new ApiResponse<BookingPaymentLinkDto>(new BookingPaymentLinkDto
            {
                Provider = "payos",
                Url = result.Url,
                Deeplink = null,
                QrCodeUrl = result.QrCodeUrl,
                TransactionId = result.OrderId,
                Status = Common.Enums.PaymentStatus.pending.ToString(),
                PaymentId = result.PaymentId
            }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPost("subscription/wallet-pay")]
    public async Task<IActionResult> PaySubscriptionByWallet(
        [FromBody] StartLandlordSubscriptionRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
        {
            return NotFound(new ApiResponse<string>("Landlord profile not found."));
        }

        try
        {
            var result = await _landlordSubscriptionService.PaySubscriptionByWalletAsync(
                landlord.LandlordId,
                dto,
                cancellationToken);

            return Ok(new ApiResponse<WalletSubscriptionPaymentResponseDto>(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpGet("payout-profile")]
    public async Task<IActionResult> GetPayoutProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var profile = await _landlordService.GetPayoutProfileAsync(landlord.LandlordId);
        return Ok(new ApiResponse<LandlordPayoutProfileDto>(profile!));
    }

    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var wallet = await _landlordWalletService.GetOrCreateAsync(landlord.LandlordId);
        var dto = new LandlordWalletBalanceDto
        {
            PendingBalance = wallet.PendingBalance,
            AvailableBalance = wallet.AvailableBalance,
            TotalBalance = wallet.PendingBalance + wallet.AvailableBalance,
            UpdatedAt = wallet.UpdatedAt
        };

        return Ok(new ApiResponse<LandlordWalletBalanceDto>(dto));
    }

    // [HttpPut("payout-profile")]
    // public async Task<IActionResult> UpdatePayoutProfile([FromBody] UpsertLandlordPayoutProfileRequestDto request)
    // {
    //     var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //     if (!Guid.TryParse(userIdClaim, out var userId))
    //     {
    //         return Unauthorized(new ApiResponse<string>("Invalid user token."));
    //     }

    //     var landlord = await _landlordService.GetByUserIdAsync(userId);
    //     if (landlord == null)
    //         return NotFound(new ApiResponse<string>("Landlord profile not found."));

    //     var profile = await _landlordService.UpsertPayoutProfileAsync(landlord.LandlordId, request);
    //     return Ok(new ApiResponse<LandlordPayoutProfileDto>(profile));
    // }

    [HttpPut("bank-payout-profile")]
    public async Task<IActionResult> UpdateBankPayoutProfile([FromBody] UpdateBankPayoutProfileRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        try
        {
            var profile = await _landlordService.UpdateBankPayoutProfileAsync(landlord.LandlordId, request);
            return Ok(new ApiResponse<LandlordPayoutProfileDto>(profile));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPut("momo-payout-profile")]
    public async Task<IActionResult> UpdateMomoPayoutProfile([FromBody] UpdateMomoPayoutProfileRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        try
        {
            var profile = await _landlordService.UpdateMomoPayoutProfileAsync(landlord.LandlordId, request);
            return Ok(new ApiResponse<LandlordPayoutProfileDto>(profile));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpPost("payouts")]
    public async Task<IActionResult> CreatePayout([FromBody] CreateLandlordPayoutRequestDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        try
        {
            var result = await _landlordPayoutService.CreatePayoutAsync(landlord.LandlordId, request, cancellationToken);
            return Ok(new ApiResponse<LandlordPayoutResponseDto>(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayoutHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? search = null,
        [FromQuery] Dictionary<string, string>? filters = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var (items, totalCount) = await _landlordPayoutService.GetPayoutHistoryAsync(
            landlord.LandlordId,
            page,
            pageSize,
            sortBy,
            sortOrder,
            search,
            filters);

        return Ok(new { Items = items, TotalCount = totalCount });
    }

    [HttpGet("payouts/{payoutId:guid}")]
    public async Task<IActionResult> GetPayoutById(Guid payoutId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        var item = await _landlordPayoutService.GetPayoutByIdAsync(landlord.LandlordId, payoutId);
        if (item == null)
            return NotFound(new ApiResponse<string>("Payout not found."));

        return Ok(new ApiResponse<LandlordPayoutResponseDto>(item));
    }

    [HttpPost("payouts/sync-processing")]
    public async Task<IActionResult> SyncProcessingPayouts(CancellationToken cancellationToken)
    {
        var updated = await _landlordPayoutService.SyncProcessingPayoutsAsync(cancellationToken);
        return Ok(new ApiResponse<int>(updated));
    }

    [HttpGet("penalties")]
    public async Task<IActionResult> GetPenalties()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<string>("Invalid user token."));
        }

        var landlord = await _landlordService.GetByUserIdAsync(userId);
        if (landlord == null)
            return NotFound(new ApiResponse<string>("Landlord profile not found."));

        try
        {
            var penalties = await _bookingService.GetLandlordOutstandingCheckTimeFeesAsync(
                landlord.LandlordId,
                userId,
                "landlord");

            return Ok(new ApiResponse<LandlordOutstandingCheckTimeFeesResponseDto>(penalties));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>(ex.Message));
        }
    }
}
