using BLL.Services.Interfaces;
using Microsoft.Extensions.Logging;
using PayOS;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public sealed class PayOsHttpAdapter : IPayOsClientAdapter
    {
        private readonly PayOSClient _client;
        private readonly ILogger<PayOsHttpAdapter>? _logger;

        public PayOsHttpAdapter(PayOSClient client, ILogger<PayOsHttpAdapter>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        public async Task<PayOS.Models.V2.PaymentRequests.CreatePaymentLinkResponse> CreatePaymentLinkAsync(PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return await _client.PaymentRequests.CreateAsync(request).ConfigureAwait(false)
                ?? throw new InvalidOperationException("PayOS returned an empty checkout response.");
        }

        public async Task<object?> GetPaymentRequestAsync(string id)
        {
            return await _client.GetAsync<object?>("/v2/payment-requests/" + Uri.EscapeDataString(id), default!).ConfigureAwait(false);
        }

        public async Task<object?> VerifyWebhookAsync(PayOS.Models.Webhooks.Webhook webhook)
        {
            if (webhook == null) return null;

            try
            {
                return await _client.Webhooks.VerifyAsync(webhook).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error verifying PayOS webhook");
                return null;
            }
        }
    }
}
