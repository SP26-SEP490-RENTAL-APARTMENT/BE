using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using PayOS.Exceptions;
using Microsoft.Extensions.Options;
using System;
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
                OrderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Amount = request.Amount,
                Description = request.OrderInfo ?? string.Empty,
                ReturnUrl = request.RedirectUrl ?? _options.RedirectUrl!,
                CancelUrl = request.CancelUrl ?? request.RedirectUrl ?? _options.RedirectUrl!,
                BuyerName = request.BuyerName,
                BuyerCompanyName = request.BuyerCompanyName,
                BuyerEmail = request.BuyerEmail,
                BuyerPhone = request.BuyerPhone,
                BuyerAddress = request.BuyerAddress,
                ExpiredAt = request.ExpiredAt?.ToUnixTimeSeconds()
            };

            if (request.Items != null && request.Items.Count > 0)
            {
                paymentRequest.Items = request.Items.Select(i => new PayOS.Models.V2.PaymentRequests.PaymentLinkItem
                {
                    Name = i.Name ?? string.Empty,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Unit = i.Unit,
                    TaxPercentage = i.TaxPercentage
                }).ToList();
            }

            if (request.BuyerNotGetInvoice.HasValue || request.TaxPercentage.HasValue)
            {
                paymentRequest.Invoice = new PayOS.Models.V2.PaymentRequests.InvoiceRequest
                {
                    BuyerNotGetInvoice = request.BuyerNotGetInvoice,
                    TaxPercentage = request.TaxPercentage
                };
            }

            try
            {
                var paymentResponse = await _sdkAdapter.CreatePaymentLinkAsync(paymentRequest).ConfigureAwait(false);

                if (paymentResponse == null)
                {
                    return new PayOsCreatePaymentResponse
                    {
                        Success = false,
                        Message = "PayOS returned an empty checkout response.",
                        Url = string.Empty,
                        QrCodeUrl = null,
                        OrderId = string.Empty,
                        RequestId = string.Empty,
                        Amount = request.Amount,
                        RequestRaw = System.Text.Json.JsonSerializer.Serialize(paymentRequest),
                        ResponseRaw = string.Empty
                    };
                }

                return new PayOsCreatePaymentResponse
                {
                    Success = true,
                    Message = paymentResponse.Status.ToString(),
                    Url = paymentResponse.CheckoutUrl ?? string.Empty,
                    QrCodeUrl = paymentResponse.QrCode,
                    OrderId = paymentResponse.PaymentLinkId ?? string.Empty,
                    RequestId = paymentRequest.OrderCode.ToString(),
                    Amount = request.Amount,
                    RequestRaw = System.Text.Json.JsonSerializer.Serialize(paymentRequest),
                    ResponseRaw = System.Text.Json.JsonSerializer.Serialize(paymentResponse)
                };
            }
            catch (ApiException ex)
            {
                return new PayOsCreatePaymentResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Url = string.Empty,
                    QrCodeUrl = null,
                    OrderId = string.Empty,
                    RequestId = string.Empty,
                    Amount = request.Amount,
                    RequestRaw = System.Text.Json.JsonSerializer.Serialize(paymentRequest),
                    ResponseRaw = ex.ToString()
                };
            }
            catch (Exception ex)
            {
                return new PayOsCreatePaymentResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Url = string.Empty,
                    QrCodeUrl = null,
                    OrderId = string.Empty,
                    RequestId = string.Empty,
                    Amount = request.Amount,
                    RequestRaw = System.Text.Json.JsonSerializer.Serialize(paymentRequest),
                    ResponseRaw = string.Empty
                };
            }
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
