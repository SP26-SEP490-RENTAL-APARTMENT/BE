using Common.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Services;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MomoController : ControllerBase
    {
        private readonly IMomoService _momoService;
        private readonly IMomoWebhookService _momoWebhookService;
        private readonly ILogger<MomoController> _logger;

        public MomoController(
            IMomoService momoService,
            IMomoWebhookService momoWebhookService,
            ILogger<MomoController> logger)
        {
            _momoService = momoService;
            _momoWebhookService = momoWebhookService;
            _logger = logger;
        }

        // IPN endpoint that MoMo will POST to (server-to-server)
        [HttpPost("ipn")]
        public async Task<IActionResult> Ipn()
        {
            var body = await ReadAndLogRawRequestAsync("ipn");
            return await _momoWebhookService.HandleIpnAsync(HttpContext, body);
        }

        [HttpPost("booking/reconcile")]
        public async Task<IActionResult> ReconcileBookingPayment([FromBody] ReconcileBookingPaymentRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return await _momoWebhookService.ReconcileBookingPaymentAsync(dto);
        }

        [HttpPost("subscription/reconcile")]
        public async Task<IActionResult> ReconcileSubscriptionPayment([FromBody] ReconcileLandlordSubscriptionPaymentRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return await _momoWebhookService.ReconcileSubscriptionPaymentAsync(dto);
        }

        [HttpPost("disbursement-ipn")]
        public async Task<IActionResult> DisbursementIpn()
        {
            var body = await ReadAndLogRawRequestAsync("disbursement-ipn");
            return await _momoWebhookService.HandleDisbursementIpnAsync(body);
        }

        [HttpPost("check-transaction-status")]
        public async Task<IActionResult> CheckTransactionStatus([FromBody] MomoQueryPaymentRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.OrderId))
            {
                return BadRequest(new { message = "orderId is required." });
            }

            _logger.LogInformation(
                "[MoMo Query] Manual transaction status check requested. orderId={OrderId}, requestId={RequestId}",
                request.OrderId,
                request.RequestId);

            var queryResult = await _momoService.QueryPaymentStatusAsync(request, cancellationToken);

            if (!string.IsNullOrWhiteSpace(queryResult.ResponseRaw))
            {
                await _momoWebhookService.ProcessQueriedPaymentAsync(queryResult.ResponseRaw, cancellationToken);
            }

            return Ok(queryResult);
        }

        private async Task<string> ReadAndLogRawRequestAsync(string endpointName)
        {
            Request.EnableBuffering();

            string body;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
            }

            Request.Body.Position = 0;

            var headers = Request.Headers
                .Select(x => $"{x.Key}={x.Value}")
                .ToArray();

            var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var sourcePort = HttpContext.Connection.RemotePort;

            _logger.LogInformation(
                "[MoMo {Endpoint}] Raw request received. SourceIp={SourceIp}, SourcePort={SourcePort}, Method={Method}, Path={Path}, Headers={Headers}, Body={Body}",
                endpointName,
                sourceIp,
                sourcePort,
                Request.Method,
                Request.Path,
                headers,
                body);

            return body;
        }
    }
}
