using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;

namespace BLL.Tests;

public class PayOsServiceTests
{
    [Fact]
    public async Task CreateCheckoutAsync_ParsesResponse()
    {
        var mockAdapter = new Mock<IPayOsClientAdapter>();
        var sdkResponse = new PayOS.Models.V2.PaymentRequests.CreatePaymentLinkResponse
        {
            CheckoutUrl = "https://payos/checkout/123",
            QrCode = "qrdata",
            PaymentLinkId = "plink123"
        };

        mockAdapter.Setup(a => a.CreatePaymentLinkAsync(It.IsAny<PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest>()))
            .ReturnsAsync(sdkResponse);

        var options = Options.Create(new PayOsOptions { RedirectUrl = "https://app/return" });
        var service = new PayOsService(mockAdapter.Object, options);

        var req = new Common.DTOs.PayOsCreatePaymentRequest { Amount = 1000, OrderInfo = "order" };
        var res = await service.CreateCheckoutAsync(req);

        Assert.True(res.Success);
        Assert.Equal("https://payos/checkout/123", res.Url);
        Assert.Equal("plink123", res.OrderId);
    }

    [Fact]
    public void VerifyWebhookSignature_ValidatesCorrectly()
    {
        var secret = "supersecret";
        var options = Options.Create(new PayOsOptions { WebhookSecret = secret });
        var mockAdapter = new Mock<IPayOsClientAdapter>();

        mockAdapter.Setup(a => a.VerifyWebhookAsync(It.IsAny<PayOS.Models.Webhooks.Webhook>()))
            .ReturnsAsync(new { Verified = true });

        var service = new PayOsService(mockAdapter.Object, options);

        var body = "{ \"order\": 123 }";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        Assert.True(service.VerifyWebhookSignature(body, hex));
    }
}

internal class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;
    public TestHttpMessageHandler(HttpResponseMessage response) => _response = response;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        return Task.FromResult(_response);
    }
}
