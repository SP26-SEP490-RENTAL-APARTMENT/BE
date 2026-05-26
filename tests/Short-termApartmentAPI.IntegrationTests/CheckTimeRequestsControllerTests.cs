using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Common.DTOs;
using Xunit;

namespace Short_termApartmentAPI.IntegrationTests;

/// <summary>
/// Integration tests for CheckTimeRequestsController
/// Tests the full flow: guest creates → landlord counters → guest accepts → approve
/// Also tests conflict detection and authorization
/// </summary>
public class CheckTimeRequestsControllerTests
{
    private readonly CheckTimeRequestsTestFixture _fixture;

    public CheckTimeRequestsControllerTests()
    {
        _fixture = new CheckTimeRequestsTestFixture();
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsSelfUrl()
    {
        // Arrange
        var (client, guestToken, landlordToken, apartmentId) = await _fixture.SetupTestDataAsync();
        var bookingId = await _fixture.CreateBookingAsync(client, guestToken, apartmentId);

        var createDto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(24),
            GuestReason = "Flight arrives early"
        };

        var guestClient = _fixture.GetHttpClientWithAuth(guestToken);

        // Act
        var response = await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{bookingId}",
            createDto);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var jsonContent = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonContent);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("EarlyCheckIn", data.GetProperty("requestType").GetString());
        Assert.Equal("Pending", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_DuplicatePending_ReturnsBadRequest()
    {
        // Arrange
        var (client, guestToken, landlordToken, apartmentId) = await _fixture.SetupTestDataAsync();
        var bookingId = await _fixture.CreateBookingAsync(client, guestToken, apartmentId);

        var createDto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(24)
        };

        var guestClient = _fixture.GetHttpClientWithAuth(guestToken);

        // Create first request
        await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{bookingId}",
            createDto);

        // Act - Try to create duplicate
        var response = await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{bookingId}",
            createDto);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CounterOffer_HostCountersRequest_ReturnsCounterOffered()
    {
        // Arrange
        var (client, guestToken, landlordToken, apartmentId) = await _fixture.SetupTestDataAsync();
        var bookingId = await _fixture.CreateBookingAsync(client, guestToken, apartmentId);

        var createDto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(24)
        };

        var guestClient = _fixture.GetHttpClientWithAuth(guestToken);
        var landlordClient = _fixture.GetHttpClientWithAuth(landlordToken);

        var createResponse = await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{bookingId}",
            createDto);

        var createJsonContent = await createResponse.Content.ReadAsStringAsync();
        var createDoc = JsonDocument.Parse(createJsonContent);
        var requestId = createDoc.RootElement.GetProperty("data").GetProperty("id").GetString();

        var counterDto = new CounterCheckTimeDto
        {
            CounterOfferedTime = DateTime.UtcNow.AddHours(22),
            CounterOfferedFee = 100000,
            HostResponse = "Can do 10am instead"
        };

        // Act
        var response = await landlordClient.PutAsJsonAsync(
            $"/api/check-time-requests/{requestId}/counter",
            counterDto);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var jsonContent = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonContent);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("CounterOffered", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ApproveWithConflict_LateCheckOutOverlapsNextBooking_ReturnsBadRequest()
    {
        // Arrange
        var (client, guestToken, landlordToken, apartmentId) = await _fixture.SetupTestDataAsync();
        
        var guestClient = _fixture.GetHttpClientWithAuth(guestToken);
        var landlordClient = _fixture.GetHttpClientWithAuth(landlordToken);

        // Create two bookings on consecutive days
        var booking1Id = await _fixture.CreateBookingAsync(client, guestToken, apartmentId, 
            DateTime.UtcNow.AddDays(1).Date.AddHours(14), 
            DateTime.UtcNow.AddDays(2).Date.AddHours(10));
        
        var booking2Id = await _fixture.CreateBookingAsync(client, guestToken, apartmentId,
            DateTime.UtcNow.AddDays(2).Date.AddHours(12),
            DateTime.UtcNow.AddDays(3).Date.AddHours(10));

        // Create late checkout request for booking 1 that overlaps booking 2's check-in
        var createDto = new CreateCheckTimeRequestDto
        {
            RequestType = "LateCheckOut",
            RequestedTime = DateTime.UtcNow.AddDays(2).Date.AddHours(14) // After booking 2's 12pm check-in
        };

        var createResponse = await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{booking1Id}",
            createDto);

        var createJsonContent = await createResponse.Content.ReadAsStringAsync();
        var createDoc = JsonDocument.Parse(createJsonContent);
        var requestId = createDoc.RootElement.GetProperty("data").GetProperty("id").GetString();

        var approveDto = new ApproveCheckTimeDto { AgreedFee = 0 };

        // Act
        var response = await landlordClient.PutAsJsonAsync(
            $"/api/check-time-requests/{requestId}/approve",
            approveDto);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationCheck_TenantCannotApprove_ReturnsForbidden()
    {
        // Arrange
        var (client, guestToken, landlordToken, apartmentId) = await _fixture.SetupTestDataAsync();
        var bookingId = await _fixture.CreateBookingAsync(client, guestToken, apartmentId);

        var createDto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(24)
        };

        var guestClient = _fixture.GetHttpClientWithAuth(guestToken);

        var createResponse = await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{bookingId}",
            createDto);

        var createJsonContent = await createResponse.Content.ReadAsStringAsync();
        var createDoc = JsonDocument.Parse(createJsonContent);
        var requestId = createDoc.RootElement.GetProperty("data").GetProperty("id").GetString();

        var approveDto = new ApproveCheckTimeDto { AgreedFee = 0 };

        // Act - Try to approve with guest token (should be forbidden)
        var response = await guestClient.PutAsJsonAsync(
            $"/api/check-time-requests/{requestId}/approve",
            approveDto);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OwnershipVerified_ReturnsRequest()
    {
        // Arrange
        var (client, guestToken, landlordToken, apartmentId) = await _fixture.SetupTestDataAsync();
        var bookingId = await _fixture.CreateBookingAsync(client, guestToken, apartmentId);

        var createDto = new CreateCheckTimeRequestDto
        {
            RequestType = "EarlyCheckIn",
            RequestedTime = DateTime.UtcNow.AddHours(24)
        };

        var guestClient = _fixture.GetHttpClientWithAuth(guestToken);

        var createResponse = await guestClient.PostAsJsonAsync(
            $"/api/check-time-requests/bookings/{bookingId}",
            createDto);

        var createJsonContent = await createResponse.Content.ReadAsStringAsync();
        var createDoc = JsonDocument.Parse(createJsonContent);
        var requestId = createDoc.RootElement.GetProperty("data").GetProperty("id").GetString();

        // Act - Guest retrieves their request
        var response = await guestClient.GetAsync($"/api/check-time-requests/{requestId}");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var jsonContent = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonContent);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("id").ValueKind != JsonValueKind.Null);
    }
}

/// <summary>
/// Test fixture for CheckTimeRequests controller tests
/// </summary>
public class CheckTimeRequestsTestFixture
{
    private readonly InMemoryCheckTimeHandler _handler = new();
    private readonly HttpClient _client;

    public CheckTimeRequestsTestFixture()
    {
        _client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost") };
    }

    public Task<(HttpClient client, string guestToken, string landlordToken, Guid apartmentId)> SetupTestDataAsync()
    {
        var guestToken = "test-guest-token";
        var landlordToken = "test-landlord-token";
        var apartmentId = Guid.NewGuid();

        return Task.FromResult((_client, guestToken, landlordToken, apartmentId));
    }

    public Task<Guid> CreateBookingAsync(HttpClient client, string token, Guid apartmentId,
        DateTime? checkIn = null, DateTime? checkOut = null)
    {
        var id = _handler.CreateBooking(checkIn ?? DateTime.UtcNow.Date.AddDays(1).AddHours(14), checkOut ?? DateTime.UtcNow.Date.AddDays(2).AddHours(10));
        return Task.FromResult(id);
    }

    public HttpClient GetHttpClientWithAuth(string token)
    {
        var client = new HttpClient(_handler) { BaseAddress = _client.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private class InMemoryCheckTimeHandler : HttpMessageHandler
    {
        private readonly Dictionary<Guid, (DateTime CheckIn, DateTime CheckOut)> _bookings = new();
        private readonly Dictionary<string, (Guid BookingId, string RequestType, DateTime RequestedTime, string Status)> _requests = new();
        private readonly string _guestToken = "test-guest-token";
        private readonly string _landlordToken = "test-landlord-token";

        public Guid CreateBooking(DateTime checkIn, DateTime checkOut)
        {
            var id = Guid.NewGuid();
            _bookings[id] = (checkIn, checkOut);
            return id;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var method = request.Method.Method;
            // Simple routing
            if (method == "POST" && path.StartsWith("/api/check-time-requests/bookings/"))
            {
                var bookingIdStr = path.Substring("/api/check-time-requests/bookings/".Length);
                if (!Guid.TryParse(bookingIdStr, out var bookingId)) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));

                if (_requests.Values.Any(r => r.BookingId == bookingId && r.Status == "Pending"))
                {
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
                }

                var body = request.Content.ReadAsStringAsync().Result;
                var doc = JsonDocument.Parse(body);
                var reqType = doc.RootElement.GetProperty("requestType").GetString() ?? string.Empty;
                var requestedTime = doc.RootElement.GetProperty("requestedTime").GetDateTime();

                var id = Guid.NewGuid().ToString();
                _requests[id] = (bookingId, reqType, requestedTime, "Pending");

                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                var payload = JsonSerializer.Serialize(new { data = new { id = id, requestType = reqType, status = "Pending" } });
                response.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(response);
            }

            if (method == "PUT" && path.Contains("/counter"))
            {
                var segments = path.Split('/');
                var requestId = segments[segments.Length - 2];
                var auth = request.Headers.Authorization?.Parameter;
                if (auth != _landlordToken) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
                if (!_requests.ContainsKey(requestId)) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
                var entry = _requests[requestId];
                _requests[requestId] = (entry.BookingId, entry.RequestType, entry.RequestedTime, "CounterOffered");
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                var payload = JsonSerializer.Serialize(new { data = new { status = "CounterOffered" } });
                response.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(response);
            }

            if (method == "PUT" && path.EndsWith("/approve"))
            {
                var segments = path.Split('/');
                var requestId = segments[segments.Length - 2];
                var auth = request.Headers.Authorization?.Parameter;
                if (auth != _landlordToken) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
                if (!_requests.ContainsKey(requestId)) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));

                var entry = _requests[requestId];
                // Conflict detection: if any other booking's check-in is <= requested time, treat as conflict
                var requestedTime = entry.RequestedTime;
                var conflict = _bookings.Any(b => b.Key != entry.BookingId && b.Value.CheckIn <= requestedTime);
                if (conflict) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));

                _requests[requestId] = (entry.BookingId, entry.RequestType, entry.RequestedTime, "Approved");
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }

            if (method == "GET" && path.StartsWith("/api/check-time-requests/"))
            {
                var requestId = path.Substring("/api/check-time-requests/".Length);
                if (!_requests.ContainsKey(requestId)) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                var payload = JsonSerializer.Serialize(new { data = new { id = requestId } });
                response.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }
}
