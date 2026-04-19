
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
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

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

            static string GetValueOrEmpty(JsonElement rootElement, string propertyName)
            {
                if (!rootElement.TryGetProperty(propertyName, out var el))
                    return string.Empty;

                var value = GetElementAsString(el);
                return value ?? string.Empty;
            }

            // Canonical IPN signature order (per MoMo):
            // accessKey, amount, extraData, message, orderId, orderInfo, orderType,
            // partnerCode, payType, requestId, responseTime, resultCode, transId

            parts.Add($"accessKey={accessKey ?? string.Empty}");
            parts.Add($"amount={GetValueOrEmpty(root, "amount")}");
            parts.Add($"extraData={GetValueOrEmpty(root, "extraData")}");
            parts.Add($"message={GetValueOrEmpty(root, "message")}");
            parts.Add($"orderId={GetValueOrEmpty(root, "orderId")}");
            parts.Add($"orderInfo={GetValueOrEmpty(root, "orderInfo")}");
            parts.Add($"orderType={GetValueOrEmpty(root, "orderType")}");
            parts.Add($"partnerCode={GetValueOrEmpty(root, "partnerCode")}");
            parts.Add($"payType={GetValueOrEmpty(root, "payType")}");
            parts.Add($"requestId={GetValueOrEmpty(root, "requestId")}");
            parts.Add($"responseTime={GetValueOrEmpty(root, "responseTime")}");
            parts.Add($"resultCode={GetValueOrEmpty(root, "resultCode")}");
            parts.Add($"transId={GetValueOrEmpty(root, "transId")}");

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

    // Canonical signature for MoMo disbursement IPN (no payType field)
    public static bool ValidateDisbursement(string requestBody, string accessKey, string secretKey)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

        if (!root.TryGetProperty("signature", out var signatureElement) || signatureElement.ValueKind != JsonValueKind.String)
            return false;

        var receivedSignature = signatureElement.GetString() ?? string.Empty;

        static string? GetElementAsString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => element.GetRawText().Trim('"')
            };
        }

        static string GetValueOrEmpty(JsonElement rootElement, string propertyName)
        {
            if (!rootElement.TryGetProperty(propertyName, out var el))
                return string.Empty;

            var value = GetElementAsString(el);
            return value ?? string.Empty;
        }

        var parts = new List<string>
        {
            $"accessKey={accessKey ?? string.Empty}",
            $"amount={GetValueOrEmpty(root, "amount")}",
            $"extraData={GetValueOrEmpty(root, "extraData")}",
            $"message={GetValueOrEmpty(root, "message")}",
            $"orderId={GetValueOrEmpty(root, "orderId")}",
            $"orderInfo={GetValueOrEmpty(root, "orderInfo")}",
            $"orderType={GetValueOrEmpty(root, "orderType")}",
            $"partnerCode={GetValueOrEmpty(root, "partnerCode")}",
            $"requestId={GetValueOrEmpty(root, "requestId")}",
            $"responseTime={GetValueOrEmpty(root, "responseTime")}",
            $"resultCode={GetValueOrEmpty(root, "resultCode")}",
            $"transId={GetValueOrEmpty(root, "transId")}",
        };

        var raw = string.Join("&", parts);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        var receivedBytes = Encoding.UTF8.GetBytes(receivedSignature);
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        if (receivedBytes.Length != computedBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(receivedBytes, computedBytes);
    }
}