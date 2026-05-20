using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoMoApi;
using Short_termApartmentAPI.Middlewares;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly IPaymentService _paymentService;
        private readonly ISupportTicketService _supportTicketService;
        private readonly IStripeService _stripeService;
        private readonly IMomoService _momoService;
        private readonly IMomoTransactionService _momoTransactionService;
        private readonly IPayOsService _payOsService;
        private readonly IImageService _imageService;
        private readonly IFptPassportRecognitionService _passportRecognitionService;
        private readonly IFptIdRecognitionService _idRecognitionService;
        private readonly MomoOptions _momoOptions;
        private readonly IResidenceReportPdfGenerator _residenceReportPdfGenerator;
        private readonly IResidenceReportDocxGenerator _residenceReportDocxGenerator;
        private readonly IMapper _mapper;

        public BookingController(
            IBookingService bookingService,
            IPaymentService paymentService,
            ISupportTicketService supportTicketService,
            IStripeService stripeService,
            IMomoService momoService,
            IMomoTransactionService momoTransactionService,
            IPayOsService payOsService,
            IImageService imageService,
            IOptions<MomoOptions> momoOptions,
            IResidenceReportPdfGenerator residenceReportPdfGenerator,
            IResidenceReportDocxGenerator residenceReportDocxGenerator,
            IMapper mapper,
            IFptPassportRecognitionService passportRecognitionService,
            IFptIdRecognitionService idRecognitionService)
        {
            _bookingService = bookingService;
            _paymentService = paymentService;
            _supportTicketService = supportTicketService;
            _stripeService = stripeService;
            _momoService = momoService;
            _momoTransactionService = momoTransactionService;
            _imageService = imageService;
            _momoOptions = momoOptions.Value;
            _payOsService = payOsService;
            _residenceReportPdfGenerator = residenceReportPdfGenerator;
            _residenceReportDocxGenerator = residenceReportDocxGenerator;
            _mapper = mapper;
            _passportRecognitionService = passportRecognitionService;
            _idRecognitionService = idRecognitionService;
        }

        public class SubmitOfflinePaymentFormDto
        {
            public string? Notes { get; set; }
            public Microsoft.AspNetCore.Http.IFormFile? Proof { get; set; }
        }

        public class ConfirmOfflinePaymentDto
        {
            public Guid PaymentId { get; set; }
            public bool Approve { get; set; }
            public string? Notes { get; set; }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _bookingService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new ApiResponse<string>("Booking not found."));
            }
            return Ok(new ApiResponse<BookingResponseDto>(_mapper.Map<BookingResponseDto>(result)));
        }

        [HttpGet]
        [Authorize(Roles = "tenant,landlord,admin,staff")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _bookingService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var mappedItems = _mapper.Map<IEnumerable<BookingResponseDto>>(items);
            return Ok(new { Items = mappedItems, TotalCount = totalCount });
        }

        [HttpPost("quote")]
        [Authorize]
        public async Task<IActionResult> Quote([FromBody] BookingQuoteRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var quote = await _bookingService.GetQuoteAsync(dto);
                return Ok(new ApiResponse<BookingQuoteResponseDto>(quote));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> Create([FromBody] CreateBookingRequestDto requestDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var rolesClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            // Block creating a new booking if tenant has outstanding unpaid booking (admins/staff/appeals can bypass)
            if (await _bookingService.HasOutstandingUnpaidBookingAsync(userId, userId, rolesClaim))
            {
                return BadRequest(new ApiResponse<string>("You have an outstanding unpaid booking. Please settle or cancel it before creating a new booking."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var created = await _bookingService.CreateWithQuoteAsync(requestDto, userId);
                var paymentLink = await CreatePaymentLinkIfRequestedAsync(created, requestDto.PaymentProvider, requestDto.DevicePlatform);
                var paymentModeText = string.Equals(created.PaymentMode, BookingPaymentMode.full.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? "full payment"
                    : "partial payment";

                var response = new CreateBookingResponseDto
                {
                    Booking = _mapper.Map<BookingResponseDto>(created),
                    PaymentLink = paymentLink
                };

                return CreatedAtAction(nameof(GetById), new { id = created.BookingId },
                    new ApiResponse<CreateBookingResponseDto>(response, $"Booking created. Please complete {paymentModeText} to confirm."));
            }
            catch (BLL.Exceptions.OutstandingUnpaidBookingException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/refund")]
        [Authorize(Roles = "tenant,staff,admin")]
        public async Task<IActionResult> Refund(Guid id, [FromBody] RequestBookingRefundDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound(new ApiResponse<string>("Booking not found."));
            }

            if (User.IsInRole("tenant") && booking.TenantId != requesterId)
            {
                return Forbid();
            }

            try
            {
                var result = await _bookingService.RefundBookingAsync(id, requesterId, dto);
                return Ok(new ApiResponse<BookingRefundResponseDto>(result, result.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/pay-balance")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> PayBalance(Guid id, [FromQuery] string? paymentProvider, [FromQuery] string? devicePlatform)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var platform = string.IsNullOrWhiteSpace(devicePlatform)
                ? (Request.Headers["User-Agent"].ToString().Contains("Android", StringComparison.OrdinalIgnoreCase) ? "android"
                   : Request.Headers["User-Agent"].ToString().Contains("iPhone", StringComparison.OrdinalIgnoreCase) || Request.Headers["User-Agent"].ToString().Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "ios"
                   : "web")
                : devicePlatform.Trim().ToLowerInvariant();

            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new ApiResponse<string>("Booking not found."));

            if (booking.TenantId != userId)
                return Forbid();

            if (booking.RemainingAmount <= 0)
                return BadRequest(new ApiResponse<string>("No remaining balance to pay."));

            if (string.IsNullOrWhiteSpace(paymentProvider))
                return BadRequest(new ApiResponse<string>("Missing paymentProvider query parameter."));

            var normalized = paymentProvider.Trim().ToLowerInvariant();

            // Build payment request for the remaining balance
            if (normalized == "stripe")
            {
                var stripeRequest = new StripeCheckoutRequestDto
                {
                    Amount = (long)Math.Round(booking.RemainingAmount),
                    DevicePlatform = platform,
                    RelatedEntityId = booking.BookingId,
                    PaymentType = PaymentTypes.balance.ToString(),
                    PaymentPurpose = PaymentPurposes.booking_balance.ToString()
                };

                var stripeResponse = await _stripeService.CreateCheckoutSessionAsync(stripeRequest);

                var payment = new Payment
                {
                    Amount = booking.RemainingAmount,
                    PaymentType = PaymentTypes.balance.ToString(),
                    PaymentPurpose = PaymentPurposes.booking_balance.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "stripe",
                    Status = PaymentStatus.pending.ToString(),
                    TransactionId = stripeResponse.SessionId
                };

                await _paymentService.CreateAsync(payment);

                return Ok(new BookingPaymentLinkDto
                {
                    Provider = "stripe",
                    Url = stripeResponse.Url,
                    Deeplink = stripeResponse.RedirectPayload.DeepLinkIos ?? stripeResponse.RedirectPayload.DeepLinkAndroid,
                    TransactionId = stripeResponse.SessionId,
                    Status = PaymentStatus.pending.ToString(),
                    PaymentId = payment.PaymentId
                });
            }

            if (normalized == "momo")
            {
                var momoRequest = new MomoCreatePaymentRequest
                {
                    Amount = (long)Math.Round(booking.RemainingAmount),
                    OrderInfo = $"Booking balance payment {booking.BookingId}",
                    ExtraData = booking.BookingId.ToString(),
                    PaymentType = PaymentTypes.balance.ToString(),
                    PaymentPurpose = PaymentPurposes.booking_balance.ToString()
                };

                var momoResponse = await _momoService.CreateWalletPaymentAsync(momoRequest);

                var payment = new Payment
                {
                    Amount = booking.RemainingAmount,
                    PaymentType = PaymentTypes.balance.ToString(),
                    PaymentPurpose = PaymentPurposes.booking_balance.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "momo",
                    Status = PaymentStatus.pending.ToString(),
                    TransactionId = momoResponse.RequestId
                };

                await _paymentService.CreateAsync(payment);

                return Ok(new BookingPaymentLinkDto
                {
                    Provider = "momo",
                    Url = momoResponse.PayUrl,
                    Deeplink = null,
                    TransactionId = momoResponse.RequestId,
                    Status = PaymentStatus.pending.ToString(),
                    PaymentId = payment.PaymentId
                });
            }

            if (normalized == "payos")
            {
                var payosRequest = new PayOsCreatePaymentRequest
                {
                    Amount = (long)Math.Round(booking.RemainingAmount),
                    OrderInfo = $"Booking balance payment {booking.BookingId}",
                    ExtraData = booking.BookingId.ToString(),
                    PaymentType = PaymentTypes.balance.ToString(),
                    PaymentPurpose = PaymentPurposes.booking_balance.ToString(),
                    RedirectUrl = ResolveMomoRedirectUrl(platform)
                };

                var payosResponse = await _payOsService.CreateCheckoutAsync(payosRequest);
                if (!payosResponse.Success)
                {
                    throw new InvalidOperationException($"PayOS checkout could not be created: {payosResponse.Message}");
                }

                var payment = new Payment
                {
                    Amount = booking.RemainingAmount,
                    PaymentType = PaymentTypes.balance.ToString(),
                    PaymentPurpose = PaymentPurposes.booking_balance.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "payos",
                    Status = PaymentStatus.pending.ToString(),
                    TransactionId = payosResponse.OrderId
                };

                await _paymentService.CreateAsync(payment);

                var requestLog = new MomoTransaction
                {
                    RequestId = payosResponse.OrderId,
                    PartnerCode = string.Empty,
                    Amount = payosResponse.Amount,
                    Type = "create_wallet_payment_payos",
                    RequestBody = System.Text.Json.JsonSerializer.Serialize(payosRequest),
                    ResponseBody = payosResponse.ResponseRaw ?? string.Empty,
                    Status = "pending",
                    ResultCode = null,
                    Message = payosResponse.Message,
                    PaymentId = payment.PaymentId,
                    CreatedAt = Common.Utils.VietnamTime.Now,
                    UpdatedAt = Common.Utils.VietnamTime.Now
                };

                await _momoTransactionService.CreateAsync(requestLog);

                return Ok(new BookingPaymentLinkDto
                {
                    Provider = "payos",
                    Url = payosResponse.Url,
                    Deeplink = payosResponse.Deeplink,
                    TransactionId = payosResponse.OrderId,
                    Status = PaymentStatus.pending.ToString(),
                    PaymentId = payment.PaymentId
                });
            }

            return BadRequest(new ApiResponse<string>("Unsupported paymentProvider."));
        }

        [HttpPost("{id:guid}/submit-offline-payment")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> SubmitOfflinePayment(Guid id, [FromForm] SubmitOfflinePaymentFormDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new ApiResponse<string>("Invalid user token."));

            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new ApiResponse<string>("Booking not found."));

            if (booking.TenantId != userId)
                return Forbid();

            // Determine payment type: deposit if deposit not paid, otherwise balance
            var isDeposit = booking.DepositPaid != true && booking.DepositAmount > 0;
            var amount = isDeposit ? booking.DepositAmount : booking.RemainingAmount;
            if (amount <= 0)
                return BadRequest(new ApiResponse<string>("No amount due for offline payment."));

            string? proofUrl = null;
            if (dto?.Proof != null)
            {
                try
                {
                    proofUrl = await _imageService.UploadImageAsync(dto.Proof);
                }
                catch
                {
                    // non-fatal: continue with null proofUrl
                    proofUrl = null;
                }
            }

            var payment = new Payment
            {
                Amount = amount,
                PaymentType = isDeposit ? PaymentTypes.deposit.ToString() : PaymentTypes.balance.ToString(),
                PaymentPurpose = isDeposit ? PaymentPurposes.booking_deposit.ToString() : PaymentPurposes.booking_balance.ToString(),
                RelatedEntityId = booking.BookingId,
                RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                Method = "offline",
                Status = PaymentStatus.pending.ToString(),
                TransactionId = null,
                ProofUrl = proofUrl,
                Notes = dto?.Notes
            };

            await _paymentService.CreateAsync(payment);

            return CreatedAtAction(nameof(GetById), new { id = booking.BookingId }, new ApiResponse<object>(new { PaymentId = payment.PaymentId, Status = payment.Status }, "Offline payment submitted and pending confirmation."));
        }

        [HttpPost("{id:guid}/confirm-offline-payment")]
        [Authorize(Roles = "landlord,admin")]
        public async Task<IActionResult> ConfirmOfflinePayment(Guid id, [FromBody] ConfirmOfflinePaymentDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterId))
                return Unauthorized(new ApiResponse<string>("Invalid user token."));

            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new ApiResponse<string>("Booking not found."));

            // If landlord role, ensure ownership
            if (User.IsInRole("landlord") && booking.Apartment != null && booking.Apartment.LandlordId != requesterId)
                return Forbid();

            // find payment
            var payment = await _paymentService.GetByIdAsync(dto.PaymentId);
            if (payment == null)
                return NotFound(new ApiResponse<string>("Payment not found."));

            if (dto.Approve)
            {
                payment.Status = PaymentStatus.success.ToString();
                payment.ConfirmedBy = requesterId;
                payment.ConfirmedAt = Common.Utils.VietnamTime.Now;
                if (!string.IsNullOrWhiteSpace(dto.Notes))
                    payment.Notes = (payment.Notes ?? string.Empty) + "\n" + dto.Notes;

                await _paymentService.UpdateAsync(payment);

                // Apply booking-side effects
                try
                {
                    if (string.Equals(payment.PaymentType, PaymentTypes.deposit.ToString(), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(payment.PaymentType, PaymentTypes.upfront.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        await _bookingService.MarkDepositPaidAsync(payment.RelatedEntityId!.Value);
                    }
                    else if (string.Equals(payment.PaymentType, PaymentTypes.balance.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        await _bookingService.MarkBalancePaidAsync(payment.RelatedEntityId!.Value);
                    }
                }
                catch { }

                return Ok(new ApiResponse<object>(new { payment.PaymentId, payment.Status }, "Offline payment confirmed and applied."));
            }
            else
            {
                payment.Status = PaymentStatus.failed.ToString();
                if (!string.IsNullOrWhiteSpace(dto.Notes))
                    payment.Notes = (payment.Notes ?? string.Empty) + "\n" + dto.Notes;
                await _paymentService.UpdateAsync(payment);
                return Ok(new ApiResponse<object>(new { payment.PaymentId, payment.Status }, "Offline payment marked as rejected."));
            }
        }

        private async Task<BookingPaymentLinkDto?> CreatePaymentLinkIfRequestedAsync(Booking booking, string? paymentProvider, string? devicePlatform)
        {
            if (string.IsNullOrWhiteSpace(paymentProvider))
            {
                return null;
            }

            var normalized = paymentProvider.Trim().ToLowerInvariant();

            if (normalized == "stripe")
            {
                var isFullPayment = string.Equals(booking.PaymentMode, BookingPaymentMode.full.ToString(), StringComparison.OrdinalIgnoreCase);
                var stripeRequest = new StripeCheckoutRequestDto
                {
                    Amount = (long)Math.Round(booking.UpfrontPaymentAmount),
                    DevicePlatform = devicePlatform,
                    RelatedEntityId = booking.BookingId,
                    PaymentType = isFullPayment ? PaymentTypes.upfront.ToString() : PaymentTypes.deposit.ToString(),
                    PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString()
                };

                var stripeResponse = await _stripeService.CreateCheckoutSessionAsync(stripeRequest);

                var payment = new Payment
                {
                    Amount = booking.UpfrontPaymentAmount,
                    PaymentType = isFullPayment ? PaymentTypes.upfront.ToString() : PaymentTypes.deposit.ToString(),
                    PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "stripe",
                    Status = PaymentStatus.pending.ToString(),
                    TransactionId = stripeResponse.SessionId
                };

                await _paymentService.CreateAsync(payment);

                return new BookingPaymentLinkDto
                {
                    Provider = "stripe",
                    Url = stripeResponse.Url,
                    Deeplink = stripeResponse.RedirectPayload.DeepLinkIos ?? stripeResponse.RedirectPayload.DeepLinkAndroid,
                    TransactionId = stripeResponse.SessionId,
                    Status = PaymentStatus.pending.ToString(),
                    PaymentId = payment.PaymentId
                };
            }

            if (normalized == "momo")
            {
                var isFullPayment = string.Equals(booking.PaymentMode, BookingPaymentMode.full.ToString(), StringComparison.OrdinalIgnoreCase);
                var momoRequest = new MomoCreatePaymentRequest
                {
                    Amount = (long)Math.Round(booking.UpfrontPaymentAmount),
                    OrderInfo = isFullPayment ? $"Booking full payment {booking.BookingId}" : $"Booking deposit {booking.BookingId}",
                    ExtraData = booking.BookingId.ToString(),
                    PaymentType = isFullPayment ? PaymentTypes.upfront.ToString() : PaymentTypes.deposit.ToString(),
                    PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString(),
                    RedirectUrl = ResolveMomoRedirectUrl(devicePlatform),
                };

                var momoResponse = await _momoService.CreateWalletPaymentAsync(momoRequest);
                if (momoResponse.ResultCode != 0)
                {
                    throw new InvalidOperationException($"MoMo checkout could not be created: {momoResponse.Message}");
                }

                var payment = new Payment
                {
                    Amount = booking.UpfrontPaymentAmount,
                    PaymentType = isFullPayment ? PaymentTypes.upfront.ToString() : PaymentTypes.deposit.ToString(),
                    PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "momo_wallet",
                    Status = PaymentStatus.pending.ToString(),
                    TransactionId = momoResponse.OrderId
                };

                await _paymentService.CreateAsync(payment);

                var requestLog = new MomoTransaction
                {
                    RequestId = momoResponse.RequestId,
                    PartnerCode = _momoOptions.PartnerCode,
                    Amount = momoResponse.Amount,
                    Type = "create_wallet_payment",
                    RequestBody = momoResponse.RequestRaw ?? string.Empty,
                    ResponseBody = momoResponse.ResponseRaw ?? string.Empty,
                    Status = "pending",
                    ResultCode = momoResponse.ResultCode,
                    Message = momoResponse.Message,
                    PaymentId = payment.PaymentId,
                    CreatedAt = Common.Utils.VietnamTime.Now,
                    UpdatedAt = Common.Utils.VietnamTime.Now
                };

                await _momoTransactionService.CreateAsync(requestLog);

                return new BookingPaymentLinkDto
                {
                    Provider = "momo",
                    Url = momoResponse.PayUrl,
                    Deeplink = momoResponse.Deeplink,
                    QrCodeUrl = momoResponse.QrCodeUrl,
                    TransactionId = momoResponse.OrderId,
                    Status = PaymentStatus.pending.ToString(),
                    PaymentId = payment.PaymentId
                };
            }

            if (normalized == "payos")
            {
                var isFullPayment = string.Equals(booking.PaymentMode, BookingPaymentMode.full.ToString(), StringComparison.OrdinalIgnoreCase);
                var payosRequest = new PayOsCreatePaymentRequest
                {
                    Amount = (long)Math.Round(booking.UpfrontPaymentAmount),
                    OrderInfo = isFullPayment ? $"Booking full payment {booking.BookingId}" : $"Booking deposit {booking.BookingId}",
                    ExtraData = booking.BookingId.ToString(),
                    PaymentType = isFullPayment ? PaymentTypes.upfront.ToString() : PaymentTypes.deposit.ToString(),
                    PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString(),
                    RedirectUrl = ResolveMomoRedirectUrl(devicePlatform)
                };

                var payosResponse = await _payOsService.CreateCheckoutAsync(payosRequest);
                if (!payosResponse.Success)
                {
                    throw new InvalidOperationException($"PayOS checkout could not be created: {payosResponse.Message}");
                }

                var payment = new Payment
                {
                    Amount = booking.UpfrontPaymentAmount,
                    PaymentType = isFullPayment ? PaymentTypes.upfront.ToString() : PaymentTypes.deposit.ToString(),
                    PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString(),
                    RelatedEntityId = booking.BookingId,
                    RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
                    Method = "payos",
                    Status = PaymentStatus.pending.ToString(),
                    TransactionId = payosResponse.OrderId
                };

                await _paymentService.CreateAsync(payment);

                var requestLog = new MomoTransaction
                {
                    RequestId = payosResponse.OrderId,
                    PartnerCode = string.Empty,
                    Amount = payosResponse.Amount,
                    Type = "create_wallet_payment_payos",
                    RequestBody = System.Text.Json.JsonSerializer.Serialize(payosRequest),
                    ResponseBody = payosResponse.ResponseRaw ?? string.Empty,
                    Status = "pending",
                    ResultCode = null,
                    Message = payosResponse.Message,
                    PaymentId = payment.PaymentId,
                    CreatedAt = Common.Utils.VietnamTime.Now,
                    UpdatedAt = Common.Utils.VietnamTime.Now
                };

                await _momoTransactionService.CreateAsync(requestLog);

                return new BookingPaymentLinkDto
                {
                    Provider = "payos",
                    Url = payosResponse.Url,
                    Deeplink = payosResponse.Deeplink,
                    TransactionId = payosResponse.OrderId,
                    Status = PaymentStatus.pending.ToString(),
                    PaymentId = payment.PaymentId
                };
            }

            return null;
        }

        private string ResolveMomoRedirectUrl(string? devicePlatform)
        {
            if (string.IsNullOrWhiteSpace(devicePlatform))
            {
                return _momoOptions.RedirectUrl;
            }

            return devicePlatform.Trim().ToLowerInvariant() switch
            {
                "android" => string.IsNullOrWhiteSpace(_momoOptions.RedirectUrlAndroid)
                    ? _momoOptions.RedirectUrl
                    : _momoOptions.RedirectUrlAndroid,
                "web" => _momoOptions.RedirectUrl,
                _ => throw new InvalidOperationException("device_platform is invalid. Supported values: ios, android, web.")
            };
        }

        private static bool TryParseDateOnly(string? rawValue, out DateOnly date)
        {
            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy" };
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                foreach (var format in formats)
                {
                    if (DateOnly.TryParseExact(rawValue, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
                    {
                        return true;
                    }
                }

                if (DateOnly.TryParse(rawValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
                {
                    return true;
                }
            }

            date = default;
            return false;
        }

        [HttpPost("{id:guid}/residence-report")]
        [Authorize(Roles = "landlord")]
        public async Task<IActionResult> SubmitResidenceReport(Guid id, [FromBody] SubmitResidenceReportDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var landlordUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var report = await _bookingService.SubmitResidenceReportAsync(id, landlordUserId, dto);
                return Ok(new ApiResponse<object>(new
                {
                    report.ReportId,
                    report.BookingId,
                    report.ReportedToPolice,
                    report.ReportDate,
                    report.ReportNumber
                }, "Residence report submitted successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("{id:guid}/residence-report/pdf")]
        [Authorize(Roles = "landlord")]
        public async Task<IActionResult> ExportResidenceReportPdf(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var landlordUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var details = await _bookingService.GetResidenceReportDetailsAsync(id, landlordUserId);
                var pdfBytes = await _residenceReportPdfGenerator.GenerateAsync(details);
                var fileName = $"residence-report-{id}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("{id:guid}/residence-report/docx")]
        [Authorize(Roles = "landlord")]
        public async Task<IActionResult> ExportResidenceReportDocx(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var landlordUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var details = await _bookingService.GetResidenceReportDetailsAsync(id, landlordUserId);
                var docxBytes = await _residenceReportDocxGenerator.GenerateAsync(details);
                var isVietnamese = string.Equals(details.TenantNationality, "VN", StringComparison.OrdinalIgnoreCase);
                var hasMultipleOccupants = (details.Occupants?.Count ?? 0) > 1;
                if (isVietnamese && hasMultipleOccupants)
                {
                    var zipName = $"residence-report-{id}.zip";
                    return File(docxBytes, "application/zip", zipName);
                }

                var fileName = $"residence-report-{id}.docx";
                return File(docxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("{id:guid}/occupants")]
        [Authorize(Roles = "tenant,landlord")]
        public async Task<IActionResult> GetOccupants(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var tenantUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var occupants = await _bookingService.GetOccupantsAsync(id, tenantUserId);
                return Ok(new ApiResponse<IReadOnlyList<ResidenceReportOccupantDto>>(occupants));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/occupants")]
        [Authorize(Roles = "tenant,landlord")]
        public async Task<IActionResult> AddOccupant(Guid id, [FromForm] AddBookingOccupantFormDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.ProofPhoto == null || dto.ProofPhoto.Length == 0)
            {
                return BadRequest(new ApiResponse<string>("Proof photo is required."));
            }

            try
            {
                var proofPhotoUrl = await _imageService.UploadImageAsync(dto.ProofPhoto);
                var occupant = await _bookingService.AddOccupantAsync(id, requesterUserId, new AddBookingOccupantDto
                {
                    FullName = dto.FullName,
                    PassportId = dto.PassportId,
                    DateOfBirth = dto.DateOfBirth,
                    NationalIdCardNumber = dto.NationalIdCardNumber,
                    Nationality = dto.Nationality,
                    Sex = dto.Sex,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    ProofPhotoUrl = proofPhotoUrl
                });
                return Ok(new ApiResponse<ResidenceReportOccupantDto>(occupant, "Occupant added successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/occupants/ocr-upload")]
        [Authorize(Roles = "tenant,landlord")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadOccupantByIdRecognition(Guid id, [FromForm] BookingOccupantOcrUploadDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.Image == null || dto.Image.Length == 0)
            {
                return BadRequest(new ApiResponse<string>("Image is required."));
            }

            FptIdRecognitionResult recognition;
            try
            {
                recognition = await _idRecognitionService.RecognizeAsync(dto.Image);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }

            string proofPhotoUrl;
            try
            {
                proofPhotoUrl = await _imageService.UploadImageAsync(dto.Image);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading image in UploadOccupantByIdRecognition: {ex}");
                return BadRequest(new ApiResponse<string>("Failed to upload image."));
            }

            var occupantDto = new AddBookingOccupantDto
            {
                FullName = string.IsNullOrWhiteSpace(recognition.FullName) ? null : recognition.FullName,
                PassportId = string.IsNullOrWhiteSpace(recognition.PassportNumber) ? null : recognition.PassportNumber,
                DateOfBirth = TryParseDateOnly(recognition.DateOfBirth, out var dob) ? dob : null,
                NationalIdCardNumber = string.IsNullOrWhiteSpace(recognition.IdNumber) ? null : recognition.IdNumber,
                Nationality = null,
                Sex = string.IsNullOrWhiteSpace(recognition.Sex) ? null : recognition.Sex,
                Phone = null,
                Email = null,
                ProofPhotoUrl = proofPhotoUrl
            };

            try
            {
                var occupant = await _bookingService.AddOccupantAsync(id, requesterUserId, occupantDto);
                return Ok(new ApiResponse<ResidenceReportOccupantDto>(occupant, "Occupant added from OCR upload."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/occupants/passport-upload")]
        [Authorize(Roles = "tenant,landlord")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadOccupantByPassport(Guid id, [FromForm] BookingOccupantOcrUploadDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.Image == null || dto.Image.Length == 0)
            {
                return BadRequest(new ApiResponse<string>("Image is required."));
            }

            // Run passport OCR first to extract fields
            FptIdRecognitionResult recognition;
            try
            {
                recognition = await _passportRecognitionService.RecognizeAsync(dto.Image);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }

            // Upload image to image service
            string proofPhotoUrl;
            try
            {
                proofPhotoUrl = await _imageService.UploadImageAsync(dto.Image);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading image in UploadOccupantByPassport: {ex}");
                return BadRequest(new ApiResponse<string>("Failed to upload image."));
            }

            // Build occupant DTO from OCR fields
            var occupantDto = new AddBookingOccupantDto
            {
                FullName = string.IsNullOrWhiteSpace(recognition.FullName) ? null : recognition.FullName,
                PassportId = string.IsNullOrWhiteSpace(recognition.PassportNumber)
                    ? (string.IsNullOrWhiteSpace(recognition.IdNumber) ? null : recognition.IdNumber)
                    : recognition.PassportNumber,
                DateOfBirth = TryParseDateOnly(recognition.DateOfBirth, out var dob) ? dob : null,
                Nationality = null,
                NationalIdCardNumber = null,
                Sex = null,
                Phone = null,
                Email = null,
                ProofPhotoUrl = proofPhotoUrl
            };

            try
            {
                var occupant = await _bookingService.AddOccupantAsync(id, requesterUserId, occupantDto);
                return Ok(new ApiResponse<ResidenceReportOccupantDto>(occupant, "Occupant added from OCR upload."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPut("{id:guid}/occupants/{occupantOrder:int}")]
        [Authorize(Roles = "tenant,landlord")]
        public async Task<IActionResult> UpdateOccupant(Guid id, int occupantOrder, [FromForm] UpdateBookingOccupantFormDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                string? proofPhotoUrl = null;
                if (dto.ProofPhoto != null && dto.ProofPhoto.Length > 0)
                {
                    proofPhotoUrl = await _imageService.UploadImageAsync(dto.ProofPhoto);
                }

                var occupant = await _bookingService.UpdateOccupantAsync(id, requesterUserId, occupantOrder, new UpdateBookingOccupantDto
                {
                    IsPrimary = dto.IsPrimary,
                    FullName = dto.FullName,
                    PassportId = dto.PassportId,
                    DateOfBirth = dto.DateOfBirth,
                    NationalIdCardNumber = dto.NationalIdCardNumber,
                    Nationality = dto.Nationality,
                    Sex = dto.Sex,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    ProofPhotoUrl = proofPhotoUrl
                });
                return Ok(new ApiResponse<ResidenceReportOccupantDto>(occupant, "Occupant updated successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpDelete("{id:guid}/occupants/{occupantOrder:int}")]
        [Authorize(Roles = "tenant,landlord")]
        public async Task<IActionResult> RemoveOccupant(Guid id, int occupantOrder)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                await _bookingService.RemoveOccupantAsync(id, requesterUserId, occupantOrder);
                return Ok(new ApiResponse<string>("Occupant removed successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPut("{id:guid}/occupants/manual")]
        [Authorize(Roles = "landlord")]
        public async Task<IActionResult> FillOccupantsManually(Guid id, [FromBody] FillBookingOccupantsDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var tenantUserId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var occupants = await _bookingService.FillOccupantsManuallyAsync(id, tenantUserId, dto);
                return Ok(new ApiResponse<IReadOnlyList<ResidenceReportOccupantDto>>(occupants, "Occupants were updated successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-in")]
        [Authorize(Roles = "landlord,staff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> RecordCheckIn(Guid id, [FromForm] Short_termApartmentAPI.DTOs.RecordCheckInFormDto form)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var recordedBy))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (form.PhotoEvidence == null || form.PhotoEvidence.Length == 0)
            {
                return BadRequest(new ApiResponse<string>("Photo evidence is required."));
            }

            try
            {
                var photoUrl = await _imageService.UploadImageAsync(form.PhotoEvidence);
                var dto = new Common.DTOs.RecordCheckInDto
                {
                    ActualCheckIn = form.ActualCheckIn,
                    Notes = form.Notes,
                    PhotoEvidenceUrl = photoUrl
                };

                var checkTimeResponse = await _bookingService.RecordCheckInAsync(id, dto, recordedBy);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Check-in recorded successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-out")]
        [Authorize(Roles = "landlord,staff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> RecordCheckOut(Guid id, [FromForm] Short_termApartmentAPI.DTOs.RecordCheckOutFormDto form)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var recordedBy))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (form.PhotoEvidence == null || form.PhotoEvidence.Length == 0)
            {
                return BadRequest(new ApiResponse<string>("Photo evidence is required."));
            }

            try
            {
                var photoUrl = await _imageService.UploadImageAsync(form.PhotoEvidence);
                var dto = new Common.DTOs.RecordCheckOutDto
                {
                    ActualCheckOut = form.ActualCheckOut,
                    Notes = form.Notes,
                    PhotoEvidenceUrl = photoUrl
                };

                var checkTimeResponse = await _bookingService.RecordCheckOutAsync(id, dto, recordedBy);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Claim opened successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("{id:guid}/check-time")]
        [Authorize]
        public async Task<IActionResult> GetCheckTimeDetails(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var checkTimeResponse = await _bookingService.GetCheckTimeDetailsAsync(id, requesterId);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/respond")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> RespondCheckTime(Guid id, [FromBody] RespondBookingCheckTimeDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var tenantId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var checkTimeResponse = await _bookingService.RespondToCheckTimeAsync(id, tenantId, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Claim response recorded successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/resolve")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> ResolveCheckTimeDispute(Guid id, [FromBody] ResolveBookingCheckTimeDisputeDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var resolvedBy))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var checkTimeResponse = await _bookingService.ResolveCheckTimeDisputeAsync(id, resolvedBy, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Claim resolved successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/settlement")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> SettleCheckTimeFee(Guid id, [FromBody] SettleBookingCheckTimeFeeDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var settledBy))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var checkTimeResponse = await _bookingService.SettleCheckTimeFeeAsync(id, settledBy, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Check-time fee settlement updated successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/payment-confirmation")]
        [Authorize(Roles = "landlord")]
        public async Task<IActionResult> SubmitPaymentConfirmation(Guid id, [FromBody] LandlordPaymentConfirmationDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var landlordId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var checkTimeResponse = await _bookingService.SubmitPaymentConfirmationAsync(id, landlordId, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Payment confirmation submitted successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/claim/pay")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> PayClaimFee(Guid id, [FromBody] PayClaimFeeDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var tenantId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var checkTimeResponse = await _bookingService.PayClaimFeeAsync(id, tenantId, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Claim fee paid successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/no-show")]
        [Authorize(Roles = "landlord,staff")]
        public async Task<IActionResult> MarkNoShow(Guid id, [FromBody] MarkNoShowDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var actorId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _bookingService.MarkNoShowAsync(id, actorId, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(response, "No-show marked successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/check-time/missing-checkout/close")]
        [Authorize(Roles = "landlord,staff,admin")]
        public async Task<IActionResult> CloseMissingCheckOut(Guid id, [FromBody] CloseMissingCheckOutDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var actorId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _bookingService.CloseMissingCheckOutAsync(id, actorId, dto);
                return Ok(new ApiResponse<BookingCheckTimeResponseDto>(response, "Missing check-out closed successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("outstanding-fees/{userId:guid}")]
        [Authorize(Roles = "tenant,staff,admin")]
        public async Task<IActionResult> GetOutstandingCheckTimeFees(Guid userId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            try
            {
                var outstandingFees = await _bookingService.GetOutstandingCheckTimeFeesAsync(userId, requesterId, userRole);
                return Ok(new ApiResponse<OutstandingCheckTimeFeesResponseDto>(outstandingFees));
            }
            catch (InvalidOperationException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("outstanding-fees/landlord/{landlordId:guid}")]
        [Authorize(Roles = "landlord,staff,admin")]
        public async Task<IActionResult> GetLandlordOutstandingCheckTimeFees(Guid landlordId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var requesterId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            try
            {
                var outstandingFees = await _bookingService.GetLandlordOutstandingCheckTimeFeesAsync(landlordId, requesterId, userRole);
                return Ok(new ApiResponse<LandlordOutstandingCheckTimeFeesResponseDto>(outstandingFees));
            }
            catch (InvalidOperationException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpGet("{id:guid}/occupied-alternatives")]
        [Authorize(Roles = "tenant,staff,admin")]
        public async Task<IActionResult> GetOccupiedAlternatives(Guid id, [FromQuery] int maxResults = 5, [FromQuery] int? radiusMeters = null)
        {
            try
            {
                if (maxResults > 10)
                {
                    maxResults = 10;
                }

                var requesterClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(requesterClaim, out var requesterId))
                {
                    return Unauthorized(new ApiResponse<string>("Invalid user token."));
                }

                if (User.IsInRole("tenant"))
                {
                    var booking = await _bookingService.GetByIdAsync(id);
                    if (booking == null || booking.TenantId != requesterId)
                    {
                        return NotFound(new ApiResponse<string>("Booking not found."));
                    }
                }

                var alternatives = await _bookingService.FindAlternativeApartmentsAsync(id, maxResults, radiusMeters);
                return Ok(new ApiResponse<IReadOnlyList<OccupiedRoomAlternativeOptionDto>>(alternatives));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }

        [HttpPost("{id:guid}/occupied-incident")]
        [Authorize(Roles = "tenant")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReportOccupiedIncident(Guid id, [FromForm] ReportOccupiedIncidentRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var requesterClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(requesterClaim, out var tenantId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null || booking.TenantId != tenantId)
            {
                return NotFound(new ApiResponse<string>("Booking not found."));
            }

            var supportTicket = new SupportTicket
            {
                TicketId = Guid.NewGuid(),
                UserId = tenantId,
                Category = "booking_issue",
                Priority = "urgent",
                Subject = $"Occupied room incident for booking {booking.BookingId}",
                Description = dto.Details,
                Status = "open",
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            };

            var created = await _supportTicketService.CreateTicketAsync(supportTicket);

            if (dto.EvidencePhotos != null && dto.EvidencePhotos.Any())
            {
                var uploadDto = new UploadSupportTicketAttachmentDto
                {
                    Files = dto.EvidencePhotos,
                    Caption = "Occupied room incident evidence",
                    IsEvidence = true
                };

                await _supportTicketService.UploadTicketAttachmentsAsync(created.TicketId, uploadDto, tenantId);
            }

            try
            {
                var alternatives = await _bookingService.FindAlternativeApartmentsAsync(id, 1);
                var firstAlternative = alternatives.FirstOrDefault();

                if (firstAlternative != null)
                {
                    var offer = await _bookingService.CreateAlternativeOfferAsync(
                        id,
                        firstAlternative.ApartmentId,
                        null,
                        "room_occupied_auto_after_incident");

                    return Ok(new ApiResponse<object>(new
                    {
                        TicketId = created.TicketId,
                        created.Status,
                        Offer = offer,
                        Message = "Incident reported. We are actively investigating and have already compiled alternative apartments for you to review."
                    }));
                }
            }
            catch (InvalidOperationException)
            {
                // Keep incident report successful even if immediate offer generation is not possible.
            }
            catch (ArgumentException)
            {
                // Keep incident report successful even if immediate offer generation is not possible.
            }

            return Ok(new ApiResponse<object>(new
            {
                TicketId = created.TicketId,
                created.Status,
                Message = "Incident reported. We are actively investigating this issue. Alternative apartments will be compiled shortly."
            }));
        }

        [HttpPost("{id:guid}/occupied-offers")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> CreateOccupiedOffer(Guid id, [FromBody] CreateBookingOfferRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var staffId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var offer = await _bookingService.CreateAlternativeOfferAsync(
                    id,
                    dto.AlternativeApartmentId,
                    staffId,
                    dto.Reason);

                return Ok(new ApiResponse<BookingOfferResponseDto>(offer, "Alternative offer created successfully."));
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

        [HttpPost("{id:guid}/occupied-incident/confirm-penalty")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> ConfirmOccupiedIncidentPenalty(Guid id, [FromBody] ConfirmOccupiedIncidentPenaltyRequestDto? dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var staffId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var result = await _bookingService.ConfirmOccupiedIncidentPenaltyAsync(
                    id,
                    staffId,
                    dto?.TicketId,
                    dto?.Notes);

                return Ok(new ApiResponse<ConfirmOccupiedIncidentPenaltyResponseDto>(result, result.Message));
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

        [HttpGet("occupied-offers/my")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> GetMyOccupiedOffers()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var tenantId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var offers = await _bookingService.GetTenantActiveOffersAsync(tenantId);
            return Ok(new ApiResponse<IReadOnlyList<BookingOfferResponseDto>>(offers));
        }

        [HttpPost("occupied-offers/{offerId:guid}/respond")]
        [Authorize(Roles = "tenant")]
        public async Task<IActionResult> RespondOccupiedOffer(Guid offerId, [FromBody] RespondBookingOfferRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var tenantId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            try
            {
                var response = await _bookingService.RespondToAlternativeOfferAsync(offerId, tenantId, dto.Accepted, dto.Notes);
                return Ok(new ApiResponse<BookingOfferResponseDto>(response, "Offer response recorded successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }
        }
    }
}
