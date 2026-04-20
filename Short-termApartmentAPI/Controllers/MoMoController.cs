using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Services;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MomoController : ControllerBase
    {
        private readonly IMomoWebhookService _momoWebhookService;
        private readonly ILogger<MomoController> _logger;

        public MomoController(
            IMomoWebhookService momoWebhookService,
            ILogger<MomoController> logger)
        {
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

        [HttpPost("webhook-listener")]
        public async Task<IActionResult> WebhookListener()
        {
            var body = await ReadAndLogRawRequestAsync("webhook-listener");
            return await _momoWebhookService.HandleWebhookListenerAsync(HttpContext, body);
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
