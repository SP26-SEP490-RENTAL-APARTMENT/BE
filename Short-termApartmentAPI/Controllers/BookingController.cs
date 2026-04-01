using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Enums;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
		private readonly IResidenceReportPdfGenerator _residenceReportPdfGenerator;
		private readonly IMapper _mapper;

		public BookingController(
			IBookingService bookingService,
			IPaymentService paymentService,
			ISupportTicketService supportTicketService,
			IResidenceReportPdfGenerator residenceReportPdfGenerator,
			IMapper mapper)
		{
			_bookingService = bookingService;
			_paymentService = paymentService;
			_supportTicketService = supportTicketService;
			_residenceReportPdfGenerator = residenceReportPdfGenerator;
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
				await CreateInitialPaymentIfRequestedAsync(created, requestDto.PaymentProvider);
				return CreatedAtAction(nameof(GetById), new { id = created.BookingId },
					new ApiResponse<BookingResponseDto>(_mapper.Map<BookingResponseDto>(created), "Booking created. Please complete deposit payment to confirm."));
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

		private async Task CreateInitialPaymentIfRequestedAsync(Booking booking, string? paymentProvider)
		{
			if (string.IsNullOrWhiteSpace(paymentProvider))
			{
				return;
			}

			var normalized = paymentProvider.Trim().ToLowerInvariant();
			var method = normalized switch
			{
				"stripe" => "stripe",
				"momo" => "momo_wallet",
				_ => null
			};

			if (method == null)
			{
				return;
			}

			var payment = new Payment
			{
				Amount = booking.DepositAmount,
				PaymentType = PaymentTypes.deposit.ToString(),
				PaymentPurpose = PaymentPurposes.booking_deposit.ToString(),
				RelatedEntityId = booking.BookingId,
				RelatedEntityType = PaymentRelatedEntityType.booking.ToString(),
				Method = method,
				Status = PaymentStatus.pending.ToString()
			};

			await _paymentService.CreateAsync(payment);
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
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			var created = await _supportTicketService.CreateTicketAsync(supportTicket);
			return Ok(new ApiResponse<object>(new
			{
				TicketId = created.TicketId,
				created.Status,
				Message = "Incident reported. Staff review is required before alternative offers are published."
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
