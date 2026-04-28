using System.Net;
using System.Net.Http;
using System.Text;
using BLL.Services.Implements;
using Common.Settings;
using Microsoft.Extensions.Options;

namespace BLL.Tests;

public class FptPassportRecognitionServiceTests
{
    [Fact]
    public async Task RecognizeAsync_WhenSuccess_ParsesPassportFields()
    {
        var responseJson = """
        {
          "errorCode": 0,
          "errorMessage": "",
          "data": [
            {
              "passport_number": "P1234567",
              "passport_number_prob": "0.98",
              "name": "JOHN DOE",
              "name_prob": "0.97",
              "dob": "01/01/1990",
              "dob_prob": "0.96",
              "pob": "Paris",
              "sex": "M",
              "id_number": "A123456",
              "doi": "01/01/2020",
              "doe": "01/01/2030"
            }
          ]
        }
        """;

        var service = CreateService((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var result = await service.RecognizeAsync(new TestFormFile("passport.jpg", "image/jpeg", new byte[] { 1, 2, 3 }));

        Assert.True(result.Success);
        Assert.Equal("P1234567", result.PassportNumber);
        Assert.Equal("JOHN DOE", result.FullName);
        Assert.Equal("01/01/1990", result.DateOfBirth);
        Assert.Equal("Paris", result.PlaceOfBirth);
        Assert.Equal("M", result.Sex);
        Assert.Equal("A123456", result.IdNumber);
        Assert.Equal("01/01/2020", result.IssueDate);
        Assert.Equal("01/01/2030", result.ExpiryDate);
        Assert.True(result.FieldConfidences.ContainsKey("passport_number_prob"));
        Assert.Equal(0.98, result.FieldConfidences["passport_number_prob"], 4);
    }

    [Fact]
    public async Task RecognizeAsync_WhenProviderReturnsErrorMessage_UsesProviderMessage()
    {
        var responseJson = """
        {
          "errorCode": 3,
          "errorMessage": "Unable to find Passport in the image",
          "data": []
        }
        """;

        var service = CreateService((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecognizeAsync(new TestFormFile("passport.jpg", "image/jpeg", new byte[] { 1, 2, 3 })));

        Assert.Equal("Unable to find Passport in the image", ex.Message);
    }

    private static FptPassportRecognitionService CreateService(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler));
        var options = Options.Create(new FptIdRecognitionOptions
        {
            PassportEndpoint = "https://api.fpt.ai/vision/passport/vnm",
            ApiKey = "test-api-key"
        });

        return new FptPassportRecognitionService(httpClient, options);
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
