using System.Net;
using System.Net.Http;
using System.Text;
using BLL.Services.Implements;
using Common.Settings;
using Microsoft.Extensions.Options;

namespace BLL.Tests;

public class FptIdRecognitionServiceTests
{
    [Fact]
    public async Task RecognizeAsync_WhenHttpStatusNotSuccess_ThrowsUnavailableMessage()
    {
        var service = CreateService((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("gateway error", Encoding.UTF8, "text/plain")
            });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecognizeAsync(new TestFormFile("id.jpg", "image/jpeg", new byte[] { 1, 2, 3 })));

        Assert.Equal("gateway error", ex.Message);
    }

    [Fact]
    public async Task RecognizeAsync_WhenProviderReturnsKnownErrorCode_MapsToActionableMessage()
    {
        var responseJson = """
        {
          "errorCode": 2,
          "errorMessage": "Failed in cropping",
          "data": []
        }
        """;

        var service = CreateService((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecognizeAsync(new TestFormFile("id.jpg", "image/jpeg", new byte[] { 1, 2, 3 })));

        Assert.Equal("Failed in cropping", ex.Message);
    }

    [Fact]
    public async Task RecognizeAsync_WhenProviderReturnsUnknownErrorCode_UsesProviderMessage()
    {
        var responseJson = """
        {
          "errorCode": 42,
          "errorMessage": "Custom provider error",
          "data": []
        }
        """;

        var service = CreateService((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecognizeAsync(new TestFormFile("id.jpg", "image/jpeg", new byte[] { 1, 2, 3 })));

        Assert.Equal("Custom provider error", ex.Message);
    }

    [Fact]
    public async Task RecognizeAsync_WhenSuccess_ParsesFieldsAndNormalizesConfidence()
    {
        var responseJson = """
        {
          "errorCode": 0,
          "errorMessage": "",
          "data": [
            {
              "type": "new",
              "type_new": "cccd_12_front",
              "id": "012345678901",
              "name": "NGUYEN VAN A",
              "dob": "01/01/2000",
                            "home": "Ho Chi Minh City",
                            "address": "123 Nguyen Trai, District 1",
                            "nationality": "Vietnamese",
                            "sex": "M",
              "issue_date": "01/01/2020",
                            "doe": "01/01/2030",
              "id_prob": "95",
              "name_prob": "0.80",
                            "dob_prob": "0.90",
                            "home_prob": "0.85",
                            "address_prob": "0.83",
                            "nationality_prob": "0.92",
                            "sex_prob": "0.88",
                            "doe_prob": "0.87"
            }
          ]
        }
        """;

        var service = CreateService((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var result = await service.RecognizeAsync(new TestFormFile("id.jpg", "image/jpeg", new byte[] { 1, 2, 3 }));

        Assert.True(result.Success);
        Assert.Equal("new", result.CardType);
        Assert.Equal("cccd_12_front", result.CardTypeDetail);
        Assert.Equal("012345678901", result.IdNumber);
        Assert.Equal("NGUYEN VAN A", result.FullName);
        Assert.Equal("01/01/2000", result.DateOfBirth);
        Assert.Equal("Ho Chi Minh City", result.Home);
        Assert.Equal("123 Nguyen Trai, District 1", result.Address);
        Assert.Equal("Vietnamese", result.Nationality);
        Assert.Equal("01/01/2020", result.IssueDate);
        Assert.Equal("01/01/2030", result.ExpiryDate);

        Assert.True(result.FieldConfidences.ContainsKey("id_prob"));
        Assert.Equal(0.95, result.FieldConfidences["id_prob"], 4);
        Assert.Equal(0.80, result.FieldConfidences["name_prob"], 4);
        Assert.Equal(0.90, result.FieldConfidences["dob_prob"], 4);
        Assert.Equal(0.85, result.FieldConfidences["home_prob"], 4);
        Assert.Equal(0.83, result.FieldConfidences["address_prob"], 4);
        Assert.Equal(0.92, result.FieldConfidences["nationality_prob"], 4);
        Assert.Equal(0.88, result.FieldConfidences["sex_prob"], 4);
        Assert.Equal(0.87, result.FieldConfidences["doe_prob"], 4);

        var expectedAverage = (0.95 + 0.80 + 0.90) / 3;
        Assert.Equal(expectedAverage, result.OverallConfidence, 4);
    }

    private static FptIdRecognitionService CreateService(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler));
        var options = Options.Create(new FptIdRecognitionOptions
        {
            Endpoint = "https://api.fpt.ai/vision/idr/vnm/",
            ApiKey = "test-api-key"
        });

        return new FptIdRecognitionService(httpClient, options);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
