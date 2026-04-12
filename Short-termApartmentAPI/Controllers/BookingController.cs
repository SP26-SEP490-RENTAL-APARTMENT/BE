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
			IOptions<MomoOptions> momoOptions,
			IResidenceReportPdfGenerator residenceReportPdfGenerator,
			IResidenceReportDocxGenerator residenceReportDocxGenerator,
			IMapper mapper)
		{
			_bookingService = bookingService;
			_paymentService = paymentService;
			_supportTicketService = supportTicketService;
			_stripeService = stripeService;
			_momoService = momoService;
			_momoTransactionService = momoTransactionService;
			_momoOptions = momoOptions.Value;
			_residenceReportPdfGenerator = residenceReportPdfGenerator;
			_residenceReportDocxGenerator = residenceReportDocxGenerator;
			_mapper = mapper;
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
		[Authorize(Roles = "tenant")]
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

			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			try
			{
				var created = await _bookingService.CreateWithQuoteAsync(requestDto, userId);
				var paymentLink = await CreatePaymentLinkIfRequestedAsync(created, requestDto.PaymentProvider);
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
			catch (InvalidOperationException ex)
			{
				return BadRequest(new ApiResponse<string>(ex.Message));
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ApiResponse<string>(ex.Message));
			}
		}

		private async Task<BookingPaymentLinkDto?> CreatePaymentLinkIfRequestedAsync(Booking booking, string? paymentProvider)
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
					PaymentPurpose = isFullPayment ? PaymentPurposes.booking_full_payment.ToString() : PaymentPurposes.booking_deposit.ToString()
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

			return null;
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

		[HttpPost("{id:guid}/check-in")]
		[Authorize(Roles = "landlord,staff")]
		public async Task<IActionResult> RecordCheckIn(Guid id, [FromBody] RecordCheckInDto dto)
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

			try
			{
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
		public async Task<IActionResult> RecordCheckOut(Guid id, [FromBody] RecordCheckOutDto dto)
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

			try
			{
				var checkTimeResponse = await _bookingService.RecordCheckOutAsync(id, dto, recordedBy);
				return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Check-out recorded successfully."));
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
				return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Check-time response recorded successfully."));
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
				return Ok(new ApiResponse<BookingCheckTimeResponseDto>(checkTimeResponse, "Check-time dispute resolved successfully."));
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

		[HttpGet("{id:guid}/occupied-alternatives")]
		[Authorize(Roles = "tenant,staff,admin")]
		public async Task<IActionResult> GetOccupiedAlternatives(Guid id, [FromQuery] int maxResults = 5)
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

				var alternatives = await _bookingService.FindAlternativeApartmentsAsync(id, maxResults);
				return Ok(new ApiResponse<IReadOnlyList<OccupiedRoomAlternativeOptionDto>>(alternatives));
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ApiResponse<string>(ex.Message));
			}
		}

		[HttpPost("{id:guid}/occupied-incident")]
		[Authorize(Roles = "tenant")]
		public async Task<IActionResult> ReportOccupiedIncident(Guid id, [FromBody] ReportOccupiedIncidentRequestDto dto)
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
						Message = "Incident reported and an alternative offer was published immediately."
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
				Message = "Incident reported. No immediate alternative could be published; staff will follow up with offers."
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
