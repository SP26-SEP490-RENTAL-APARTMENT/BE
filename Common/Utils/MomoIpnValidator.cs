
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MoMoApi.Services;

public static class MomoIpnValidator
{
    // Validates IPN payload using AccessKey + SecretKey. Returns true when signature matches.
    // NOTE: BuildRawSignature must match MoMo's exact canonical field order for IPN callbacks.
    public static bool Validate(string requestBody, string accessKey, string secretKey)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        // Extract signature from payload (adjust if MoMo sends it in header)
        if (!root.TryGetProperty("signature", out var signatureElement) || signatureElement.ValueKind != JsonValueKind.String)
            return false;

        var receivedSignature = signatureElement.GetString() ?? string.Empty;

        // Build canonical raw string in the exact order MoMo expects for IPN.
        string BuildRawSignature()
        {
            var parts = new List<string>();

            static string? GetElementAsString(JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => element.GetRawText().Trim('\"')
                };
            }

            void AddPart(string propertyName)
            {
                if (!root.TryGetProperty(propertyName, out var el))
                    return;

                var value = GetElementAsString(el);
                if (!string.IsNullOrEmpty(value))
                {
                    parts.Add($"{propertyName}={value}");
                }
            }

            // Canonical IPN signature order (per MoMo):
            // accessKey, amount, extraData, message, orderId, orderInfo, orderType,
            // partnerCode, payType, requestId, responseTime, resultCode, transId

            if (!string.IsNullOrEmpty(accessKey))
            {
                parts.Add($"accessKey={accessKey}");
            }

            AddPart("amount");
            AddPart("extraData");
            AddPart("message");
            AddPart("orderId");
            AddPart("orderInfo");
            AddPart("orderType");
            AddPart("partnerCode");
            AddPart("payType");
            AddPart("requestId");
            AddPart("responseTime");
            AddPart("resultCode");
            AddPart("transId");

            return string.Join("&", parts);
        }

        var raw = BuildRawSignature();

        // Compute HMAC-SHA256 and hex lower-case
        var secretBytes = Encoding.UTF8.GetBytes(secretKey);
        var rawBytes = Encoding.UTF8.GetBytes(raw);
        using var hmac = new HMACSHA256(secretBytes);
        var computed = hmac.ComputeHash(rawBytes);
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        // Constant-time compare
        var receivedBytes = Encoding.UTF8.GetBytes(receivedSignature);
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        if (receivedBytes.Length != computedBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(receivedBytes, computedBytes);
    }
}