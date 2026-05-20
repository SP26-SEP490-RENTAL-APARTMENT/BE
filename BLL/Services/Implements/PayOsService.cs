using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class PayOsService : IPayOsService
    {
        private readonly PayOsOptions _options;
        private readonly IPayOsClientAdapter _sdkAdapter;

        public PayOsService(IPayOsClientAdapter sdkAdapter, IOptions<PayOsOptions> options)
        {
            _sdkAdapter = sdkAdapter ?? throw new ArgumentNullException(nameof(sdkAdapter));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<PayOsCreatePaymentResponse> CreateCheckoutAsync(PayOsCreatePaymentRequest request, CancellationToken cancellationToken = default)
        {
            var paymentRequest = new PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest
            {
                OrderCode = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Amount = request.Amount,
                Description = request.OrderInfo ?? string.Empty,
                ReturnUrl = request.RedirectUrl ?? _options.RedirectUrl,
                CancelUrl = _options.RedirectUrl
            };

            var paymentResponse = await _sdkAdapter.CreatePaymentLinkAsync(paymentRequest).ConfigureAwait(false);

            var result = new PayOsCreatePaymentResponse
            {
                Success = true,
                Url = paymentResponse.CheckoutUrl,
                QrCodeUrl = paymentResponse.QrCode,
                OrderId = paymentResponse.PaymentLinkId,
                RequestId = string.Empty,
                Amount = request.Amount,
                ResponseRaw = System.Text.Json.JsonSerializer.Serialize(paymentResponse)
            };

            return result;
        }

        public async Task<string> QueryPaymentStatusAsync(string orderId, CancellationToken cancellationToken = default)
        {
            var paymentLink = await _sdkAdapter.GetPaymentRequestAsync(orderId).ConfigureAwait(false);
            return System.Text.Json.JsonSerializer.Serialize(paymentLink);
        }

        public bool VerifyWebhookSignature(string requestBody, string signatureHeader)
        {
            try
            {
                var webhook = System.Text.Json.JsonSerializer.Deserialize<PayOS.Models.Webhooks.Webhook>(requestBody ?? string.Empty);
                var verified = _sdkAdapter.VerifyWebhookAsync(webhook!).GetAwaiter().GetResult();
                return verified != null;
            }
            catch
            {
                return false;
            }
        }

        private static string? GetJsonString(System.Text.Json.JsonElement root, params string[] names)
        {
            foreach (var n in names)
            {
                if (root.TryGetProperty(n, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return prop.GetString();
                }
            }

            return null;
        }
    }
}
