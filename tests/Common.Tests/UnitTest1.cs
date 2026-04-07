using Common.Utils;
using MoMoApi.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Common.Tests;

public class EmailVerifierTests
{
    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("USER@example.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("user@", false)]
    public void IsValidFormat_returns_expected_result(string email, bool expected)
    {
        var result = EmailVerifier.IsValidFormat(email);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user@mailinator.com", true)]
    [InlineData("user@sub.mailinator.com", true)]
    [InlineData("user@example.com", true)]
    [InlineData("user@company.com", false)]
    public void IsDisposableDomain_returns_expected_result(string email, bool expected)
    {
        var result = EmailVerifier.IsDisposableDomain(email);

        Assert.Equal(expected, result);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_produces_a_different_value_than_input()
    {
        const string password = "P@ssw0rd!";

        var hashed = PasswordHasher.HashPassword(password);

        Assert.False(string.IsNullOrWhiteSpace(hashed));
        Assert.NotEqual(password, hashed);
    }

    [Fact]
    public void VerifyPassword_returns_true_for_correct_password_and_false_for_wrong_password()
    {
        const string password = "P@ssw0rd!";

        var hashed = PasswordHasher.HashPassword(password);

        Assert.True(PasswordHasher.VerifyPassword(password, hashed));
        Assert.False(PasswordHasher.VerifyPassword("wrong", hashed));
    }
}

public class MomoIpnValidatorTests
{
    [Fact]
    public void Validate_returns_true_for_valid_signature()
    {
        const string accessKey = "access-key";
        const string secretKey = "secret-key";

        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 125000,
            ["extraData"] = "",
            ["message"] = "Successful.",
            ["orderId"] = "ORD-100",
            ["orderInfo"] = "Payment",
            ["orderType"] = "momo_wallet",
            ["partnerCode"] = "MOMO",
            ["payType"] = "qr",
            ["requestId"] = "REQ-100",
            ["responseTime"] = 1710000000,
            ["resultCode"] = 0,
            ["transId"] = 9876543210
        };

        var raw = string.Join("&", new[]
        {
            $"accessKey={accessKey}",
            "amount=125000",
            "message=Successful.",
            "orderId=ORD-100",
            "orderInfo=Payment",
            "orderType=momo_wallet",
            "partnerCode=MOMO",
            "payType=qr",
            "requestId=REQ-100",
            "responseTime=1710000000",
            "resultCode=0",
            "transId=9876543210"
        });

        payload["signature"] = ComputeHmacSha256Hex(raw, secretKey);
        var body = JsonSerializer.Serialize(payload);

        var result = MomoIpnValidator.Validate(body, accessKey, secretKey);

        Assert.True(result);
    }

    [Fact]
    public void ValidateDisbursement_returns_false_for_tampered_signature()
    {
        const string accessKey = "access-key";
        const string secretKey = "secret-key";

        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 250000,
            ["extraData"] = "",
            ["message"] = "Successful.",
            ["orderId"] = "ORD-200",
            ["orderInfo"] = "Disbursement",
            ["orderType"] = "momo_wallet",
            ["partnerCode"] = "MOMO",
            ["requestId"] = "REQ-200",
            ["responseTime"] = 1710000001,
            ["resultCode"] = 0,
            ["transId"] = 1234567890,
            ["signature"] = "invalid-signature"
        };

        var body = JsonSerializer.Serialize(payload);

        var result = MomoIpnValidator.ValidateDisbursement(body, accessKey, secretKey);

        Assert.False(result);
    }

    private static string ComputeHmacSha256Hex(string raw, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
