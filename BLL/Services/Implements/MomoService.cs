using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.Extensions.Options;
using MoMoApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class MomoService : IMomoService
    {
        private readonly HttpClient _httpClient;
        private readonly MomoOptions _options;

        public MomoService(HttpClient httpClient, IOptions<MomoOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default)
        {
            var orderId = _options.PartnerCode + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var requestId = orderId;
            var amountString = request.Amount.ToString();
            var extraData = request.ExtraData ?? string.Empty;

            var rawSignature =
                $"accessKey={_options.AccessKey}" +
                $"&amount={amountString}" +
                $"&extraData={extraData}" +
                $"&ipnUrl={_options.IpnUrl}" +
                $"&orderId={orderId}" +
                $"&orderInfo={request.OrderInfo}" +
                $"&partnerCode={_options.PartnerCode}" +
                $"&redirectUrl={_options.RedirectUrl}" +
                $"&requestId={requestId}" +
                "&requestType=captureWallet";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SecretKey));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawSignature));
            var signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

            var body = new
            {
                partnerCode = _options.PartnerCode,
                accessKey = _options.AccessKey,
                requestId,
                amount = amountString,
                orderId,
                orderInfo = request.OrderInfo,
                redirectUrl = _options.RedirectUrl,
                ipnUrl = _options.IpnUrl,
                extraData,
                requestType = "captureWallet",
                signature,
                lang = "en"
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.Endpoint), "/v2/gateway/api/create"))
            {
                Content = content
            };
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var result = new MomoCreatePaymentResponse
            {
                OrderId = orderId,
                RequestId = requestId,
                Amount = request.Amount,
                RequestRaw = json,
                ResponseRaw = responseBody,
                ResultCode = root.TryGetProperty("resultCode", out var rc) && rc.ValueKind == JsonValueKind.Number
                    ? rc.GetInt32()
                    : -1,
                Message = root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String
                    ? msg.GetString() ?? string.Empty
                    : string.Empty,
                PayUrl = root.TryGetProperty("payUrl", out var pay) && pay.ValueKind == JsonValueKind.String
                    ? pay.GetString()
                    : null,
                Deeplink = root.TryGetProperty("deeplink", out var dl) && dl.ValueKind == JsonValueKind.String
                    ? dl.GetString()
                    : null,
                QrCodeUrl = root.TryGetProperty("qrCodeUrl", out var qr) && qr.ValueKind == JsonValueKind.String
                    ? qr.GetString()
                    : null
            };

            return result;
        }
    }

}
