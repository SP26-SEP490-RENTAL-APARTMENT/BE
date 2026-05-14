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
    private readonly HttpClient _client;
    private readonly string _baseUrl = "http://localhost:5000";

    public CheckTimeRequestsTestFixture()
    {
        _client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    public async Task<(HttpClient client, string guestToken, string landlordToken, Guid apartmentId)> SetupTestDataAsync()
    {
        // TODO: Implement test data setup
        // In a real scenario, this would:
        // 1. Create a test user (guest)
        // 2. Create a test user (landlord)
        // 3. Create an apartment
        // 4. Get JWT tokens for both users
        // 5. Return everything needed for tests

        var guestToken = "test-guest-token";
        var landlordToken = "test-landlord-token";
        var apartmentId = Guid.NewGuid();

        return (_client, guestToken, landlordToken, apartmentId);
    }

    public async Task<Guid> CreateBookingAsync(HttpClient client, string token, Guid apartmentId,
        DateTime? checkIn = null, DateTime? checkOut = null)
    {
        // TODO: Implement booking creation
        return Guid.NewGuid();
    }

    public HttpClient GetHttpClientWithAuth(string token)
    {
        var client = new HttpClient { BaseAddress = _client.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
