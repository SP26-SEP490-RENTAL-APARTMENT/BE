using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.Extensions.Options;
using MoMoApi;
using MoMoApi.Services;
using System;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        public bool ValidateDisbursementIpnSignature(string requestBody)
        {
            return MomoIpnValidator.ValidateDisbursement(requestBody, _options.AccessKey, _options.SecretKey);
        }

        public async Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default)
        {
            var orderId = _options.PartnerCode + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var requestId = orderId;
            var amountString = request.Amount.ToString();
            var extraData = request.ExtraData ?? string.Empty;
            var redirectUrl = string.IsNullOrWhiteSpace(request.RedirectUrl) ? _options.RedirectUrl : request.RedirectUrl;

            Console.WriteLine($"[MoMo] CreateWalletPayment request prepared. orderId={orderId}, requestId={requestId}, ipnUrl={_options.IpnUrl}, redirectUrl={redirectUrl}, endpoint={_options.Endpoint}");

            var rawSignature =
                $"accessKey={_options.AccessKey}" +
                $"&amount={amountString}" +
                $"&extraData={extraData}" +
                $"&ipnUrl={_options.IpnUrl}" +
                $"&orderId={orderId}" +
                $"&orderInfo={request.OrderInfo}" +
                $"&partnerCode={_options.PartnerCode}" +
                $"&redirectUrl={redirectUrl}" +
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
                redirectUrl,
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

            Console.WriteLine($"[MoMo] CreateWalletPayment response received. orderId={orderId}, requestId={requestId}, httpStatus={(int)response.StatusCode}");

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

        public async Task<MomoDisbursementResponse> VerifyWalletAsync(MomoVerifyWalletRequest request, CancellationToken cancellationToken = default)
        {
            var orderId = _options.PartnerCode + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var requestId = "VW" + Guid.NewGuid().ToString("N")[..20];
            var requestType = "checkWallet";

            var disbursementMethodPlain = JsonSerializer.Serialize(new
            {
                walletId = request.WalletId,
                walletName = request.WalletName,
                personalId = request.PersonalId
            });
            var encryptedMethod = EncryptWithPublicKey(disbursementMethodPlain, _options.PublicKey);

            var rawSignature =
                $"accessKey={_options.AccessKey}" +
                $"&disbursementMethod={encryptedMethod}" +
                $"&orderId={orderId}" +
                $"&partnerCode={_options.PartnerCode}" +
                $"&requestId={requestId}" +
                $"&requestType={requestType}";

            var signature = ComputeHmac(rawSignature, _options.SecretKey);

            var payload = new
            {
                partnerCode = _options.PartnerCode,
                orderId,
                requestId,
                requestType,
                disbursementMethod = encryptedMethod,
                lang = string.IsNullOrWhiteSpace(request.Lang) ? "vi" : request.Lang,
                signature
            };

            var requestRaw = JsonSerializer.Serialize(payload);
            var root = await PostJsonAsync("/v2/gateway/api/disbursement/verify", requestRaw, cancellationToken);

            return new MomoDisbursementResponse
            {
                OrderId = orderId,
                RequestId = requestId,
                ResultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1,
                Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty,
                RequestRaw = requestRaw,
                ResponseRaw = root.GetRawText()
            };
        }

        public async Task<MomoDisbursementResponse> CreateDisbursementAsync(MomoDisbursementRequest request, CancellationToken cancellationToken = default)
        {
            var orderId = _options.PartnerCode + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var requestId = "DP" + Guid.NewGuid().ToString("N")[..20];
            var extraData = request.ExtraData ?? string.Empty;
            var orderInfo = string.IsNullOrWhiteSpace(request.OrderInfo) ? "Landlord payout" : request.OrderInfo;
            var requestType = string.IsNullOrWhiteSpace(request.RequestType) ? "disburseToWallet" : request.RequestType;

            var methodPlain = requestType.Equals("disburseToBank", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Serialize(new
                {
                    bankAccountNo = request.ReceiverAccount,
                    bankAccountHolderName = request.ReceiverName,
                    bankCode = request.BankCode
                })
                : JsonSerializer.Serialize(new
                {
                    walletId = request.ReceiverAccount,
                    walletName = request.ReceiverName,
                    personalId = request.PersonalId
                });

            var encryptedMethod = EncryptWithPublicKey(methodPlain, _options.PublicKey);
            var amountString = request.Amount.ToString();

            var rawSignature =
                $"accessKey={_options.AccessKey}" +
                $"&amount={amountString}" +
                $"&disbursementMethod={encryptedMethod}" +
                $"&extraData={extraData}" +
                $"&orderId={orderId}" +
                $"&orderInfo={orderInfo}" +
                $"&partnerCode={_options.PartnerCode}" +
                $"&requestId={requestId}" +
                $"&requestType={requestType}";

            var signature = ComputeHmac(rawSignature, _options.SecretKey);

            var payload = new
            {
                partnerCode = _options.PartnerCode,
                storeId = _options.StoreId,
                orderId,
                amount = amountString,
                requestId,
                requestType,
                disbursementMethod = encryptedMethod,
                ipnUrl = _options.DisbursementIpnUrl,
                extraData,
                orderInfo,
                orderGroupId = _options.OrderGroupId,
                lang = string.IsNullOrWhiteSpace(request.Lang) ? "vi" : request.Lang,
                signature
            };

            var requestRaw = JsonSerializer.Serialize(payload);
            var root = await PostJsonAsync("/v2/gateway/api/disbursement/pay", requestRaw, cancellationToken, 30);

            return new MomoDisbursementResponse
            {
                OrderId = orderId,
                RequestId = requestId,
                Amount = request.Amount,
                ResultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1,
                Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty,
                TransId = root.TryGetProperty("transId", out var tx) ? tx.GetRawText().Trim('"') : null,
                Balance = root.TryGetProperty("balance", out var bal) && bal.ValueKind == JsonValueKind.Number ? bal.GetInt64() : null,
                RequestRaw = requestRaw,
                ResponseRaw = root.GetRawText()
            };
        }

        public async Task<MomoQueryDisbursementResponse> QueryDisbursementStatusAsync(MomoQueryDisbursementRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = request.RequestId ?? ("QS" + Guid.NewGuid().ToString("N")[..20]);

            var rawSignature =
                $"accessKey={_options.AccessKey}" +
                $"&orderId={request.OrderId}" +
                $"&partnerCode={_options.PartnerCode}" +
                $"&requestId={requestId}";

            var signature = ComputeHmac(rawSignature, _options.SecretKey);

            var payload = new
            {
                partnerCode = _options.PartnerCode,
                orderId = request.OrderId,
                requestId,
                lang = string.IsNullOrWhiteSpace(request.Lang) ? "vi" : request.Lang,
                signature
            };

            var requestRaw = JsonSerializer.Serialize(payload);
            var root = await PostJsonAsync("/v2/gateway/api/query", requestRaw, cancellationToken);

            return new MomoQueryDisbursementResponse
            {
                OrderId = request.OrderId,
                RequestId = requestId,
                ResultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1,
                Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty,
                TransId = root.TryGetProperty("transId", out var tx) ? tx.GetRawText().Trim('"') : null,
                ResponseRaw = root.GetRawText()
            };
        }

        private static string ComputeHmac(string raw, string secretKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string EncryptWithPublicKey(string plainText, string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new InvalidOperationException("MoMo public key is missing.");
            }

            using var rsa = RSA.Create();
            if (publicKey.Contains("BEGIN PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
            {
                rsa.ImportFromPem(publicKey);
            }
            else
            {
                var keyBytes = Convert.FromBase64String(publicKey.Trim());
                rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            }

            var encrypted = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(encrypted);
        }

        private async Task<JsonElement> PostJsonAsync(string path, string json, CancellationToken cancellationToken, int? timeoutSeconds = null)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.Endpoint), path))
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            if (timeoutSeconds.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
                response = await _httpClient.SendAsync(request, cts.Token);
            }
            else
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.Clone();
        }
    }

}
