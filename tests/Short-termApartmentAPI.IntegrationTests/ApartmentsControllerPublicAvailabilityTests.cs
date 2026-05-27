using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Short_termApartmentAPI.Controllers;

namespace Short_termApartmentAPI.IntegrationTests;

public class ApartmentsControllerPublicAvailabilityTests
{
    [Fact]
    public async Task GetAllPublic_ReturnsBadRequest_WhenOnlyOneAvailabilityDateProvided()
    {
        var apartmentService = new ApartmentServiceStub();
        var controller = CreateController(apartmentService);

        var result = await controller.GetAllPublic(checkInDate: "2026-05-10", checkOutDate: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<Short_termApartmentAPI.Middlewares.ApiResponse<string>>(badRequest.Value);
        Assert.Equal("Both checkInDate and checkOutDate are required when searching by availability period.", response.Message);
    }

    [Fact]
    public async Task GetAllPublic_ReturnsBadRequest_WhenDateFormatIsInvalid()
    {
        var apartmentService = new ApartmentServiceStub();
        var controller = CreateController(apartmentService);

        var result = await controller.GetAllPublic(checkInDate: "2026-13-10", checkOutDate: "2026-05-12");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<Short_termApartmentAPI.Middlewares.ApiResponse<string>>(badRequest.Value);
        Assert.Equal("Invalid checkInDate. Use a valid date format (yyyy-MM-dd).", response.Message);
    }

    [Fact]
    public async Task GetAllPublic_ReturnsBadRequest_WhenDateRangeIsInvalid()
    {
        var apartmentService = new ApartmentServiceStub();
        var controller = CreateController(apartmentService);

        var result = await controller.GetAllPublic(checkInDate: "2026-05-12", checkOutDate: "2026-05-12");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<Short_termApartmentAPI.Middlewares.ApiResponse<string>>(badRequest.Value);
        Assert.Equal("checkOutDate must be later than checkInDate.", response.Message);
    }

    [Fact]
    public async Task GetAllPublic_PassesAvailabilityDatesAndKeepsDateKeysOutOfFilters()
    {
        var apartmentService = new ApartmentServiceStub
        {
            PublicResponse = (new[] { new ApartmentResponseDto { ApartmentId = Guid.NewGuid(), Title = "A" } }, 1)
        };

        var controller = CreateController(apartmentService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        controller.HttpContext.Request.QueryString = new QueryString("?city=Hanoi&checkInDate=2026-05-10&checkOutDate=2026-05-12");

        var result = await controller.GetAllPublic(checkInDate: "2026-05-10", checkOutDate: "2026-05-12");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(new DateOnly(2026, 5, 10), apartmentService.LastCheckInDate);
        Assert.Equal(new DateOnly(2026, 5, 12), apartmentService.LastCheckOutDate);
        Assert.NotNull(apartmentService.LastFilters);
        Assert.Equal("Hanoi", apartmentService.LastFilters!["city"]);
        Assert.False(apartmentService.LastFilters.ContainsKey("checkInDate"));
        Assert.False(apartmentService.LastFilters.ContainsKey("checkOutDate"));
    }

    private static ApartmentsController CreateController(ApartmentServiceStub apartmentService)
    {
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        return new ApartmentsController(
            apartmentService,
            new LandlordServiceStub(),
            new BookingServiceStub(),
            new SmartPricingHistoryServiceStub(),
            mapper)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private sealed class ApartmentServiceStub : IApartmentService
    {
        public Dictionary<string, string>? LastFilters { get; private set; }
        public DateOnly? LastCheckInDate { get; private set; }
        public DateOnly? LastCheckOutDate { get; private set; }
        public (IEnumerable<ApartmentResponseDto> Items, int TotalCount) PublicResponse { get; set; } = (Array.Empty<ApartmentResponseDto>(), 0);

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, DateOnly? checkInDate = null, DateOnly? checkOutDate = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            IEnumerable<string>? allowedColumns = null,
            Dictionary<string, string>? filters = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(
            int page,
            int pageSize,
            Guid landlordId,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            IEnumerable<string>? allowedColumns = null,
            Dictionary<string, string>? filters = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        // Intentionally rely on the implementation below that captures filters and dates

        public Task<(IEnumerable<ApartmentResponseDto> Items, int TotalCount)> GetAllPublicResponseAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, Guid? tenantId = null, DateOnly? checkInDate = null, DateOnly? checkOutDate = null)
        {
            LastFilters = filters;
            LastCheckInDate = checkInDate;
            LastCheckOutDate = checkOutDate;
            return Task.FromResult(PublicResponse);
        }

        public Task<CreateApartmentResponseDto> CreateApartmentWithPhotosAsync(CreateApartmentRequestDto requestDto, Guid landlordId) => throw new NotImplementedException();
        public Task<ApartmentResponseDto?> GetApartmentWithDetailsAsync(Guid id) => throw new NotImplementedException();
        public Task<ApartmentResponseDto?> GetApartmentWithDetailsResponseAsync(Guid id, Guid? tenantId = null) => throw new NotImplementedException();
        public Task AddAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds) => throw new NotImplementedException();
        public Task RemoveAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds) => throw new NotImplementedException();
        public Task UpdateApartmentPhotosAsync(Guid apartmentId, List<IFormFile> photos) => throw new NotImplementedException();
        public Task<Apartment> SubmitForReviewAsync(Guid apartmentId, Guid landlordId, SubmitForReviewDto dto) => throw new NotImplementedException();
        public Task<Apartment> ApproveListingAsync(Guid apartmentId, Guid adminId, ApproveListingDto dto) => throw new NotImplementedException();
        public Task<Apartment> UnpublishApartmentAsync(Guid apartmentId, Guid requesterId, string? reason = null) => throw new NotImplementedException();
        public Task<bool> ValidateListingDetailsAsync(Guid apartmentId) => throw new NotImplementedException();
        public Task<Apartment?> GetByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => throw new NotImplementedException();
        public Task<Apartment> CreateAsync(Apartment entity) => throw new NotImplementedException();
        public Task UpdateAsync(Apartment entity) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id) => throw new NotImplementedException();
    }

    private sealed class SmartPricingHistoryServiceStub : ISmartPricingHistoryService
    {
        public Task<SmartPricingHistory> SuggestPriceAsync(Guid apartmentId, DateOnly date, decimal? occupancyRate = null) => throw new NotImplementedException();
        public Task<SmartPricingHistory> SuggestPriceAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate, decimal? occupancyRate = null) => throw new NotImplementedException();
        public Task<SmartPricingHistory> AcceptPriceSuggestionAsync(Guid pricingId, decimal? overridePrice = null) => throw new NotImplementedException();
        public Task<bool> HasAcceptedSuggestionAsync(Guid apartmentId) => Task.FromResult(false);
        public Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetAllSuggestionsAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null) => Task.FromResult((Enumerable.Empty<SmartPricingHistory>(), 0));
        public Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetSuggestionsForLandlordAsync(Guid landlordId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null) => Task.FromResult((Enumerable.Empty<SmartPricingHistory>(), 0));
        public Task<SmartPricingHistory?> GetByIdAsync(Guid id) => Task.FromResult<SmartPricingHistory?>(null);
        public Task<(IEnumerable<SmartPricingHistory> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<SmartPricingHistory>(), 0));
        public Task<SmartPricingHistory> CreateAsync(SmartPricingHistory entity) => Task.FromResult(entity);
        public Task UpdateAsync(SmartPricingHistory entity) => Task.CompletedTask;
        public Task DeleteAsync(Guid id) => Task.CompletedTask;
    }
}