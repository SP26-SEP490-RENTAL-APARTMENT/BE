using BLL.Services.Interfaces;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class PayOsSdkAdapter : IPayOsClientAdapter
    {
        private readonly PayOS.PayOSClient _client;

        public PayOsSdkAdapter(PayOS.PayOSClient client)
        {
            _client = client;
        }

        public async Task<PayOS.Models.V2.PaymentRequests.CreatePaymentLinkResponse> CreatePaymentLinkAsync(PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest request)
        {
            return await _client.PaymentRequests.CreateAsync(request).ConfigureAwait(false);
        }

        public async Task<object?> GetPaymentRequestAsync(string id)
        {
            var res = await _client.PaymentRequests.GetAsync(id).ConfigureAwait(false);
            return res;
        }

        public async Task<object?> VerifyWebhookAsync(PayOS.Models.Webhooks.Webhook webhook)
        {
            var res = await _client.Webhooks.VerifyAsync(webhook).ConfigureAwait(false);
            return res;
        }
    }
}
