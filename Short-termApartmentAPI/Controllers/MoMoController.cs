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

        public MomoController(
            IMomoWebhookService momoWebhookService)
        {
            _momoWebhookService = momoWebhookService;
        }

        // IPN endpoint that MoMo will POST to (server-to-server)
        [HttpPost("ipn")]
        public async Task<IActionResult> Ipn()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            return await _momoWebhookService.HandleIpnAsync(HttpContext, body);
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
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            return await _momoWebhookService.HandleDisbursementIpnAsync(body);
        }
    }
}
