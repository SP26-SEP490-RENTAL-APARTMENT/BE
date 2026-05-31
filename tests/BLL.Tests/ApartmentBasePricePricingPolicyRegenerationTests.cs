using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Xunit;

namespace BLL.Tests;

public class ApartmentBasePricePricingPolicyRegenerationTests
{
    [Fact]
    public async Task UpdateAsync_RegeneratesPricingPolicyRows_WhenBasePriceChangesOnTrackedApartment()
    {
        var apartmentId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = landlordId,
            Title = "Test Apartment",
            City = "Hanoi",
            Status = "posted",
            BookingStatus = "available",
            BasePricePerNight = 100m,
            Location = new Point(0, 0)
        };

        var apartmentRepository = new TrackedApartmentRepository(apartment);
        var applicationRepository = new InMemoryRepository<ApartmentPricingPolicyApplication>(null, new ApartmentPricingPolicyApplication
        {
            ApplicationId = applicationId,
            ApartmentId = apartmentId,
            TemplateId = templateId,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 3),
            IsEnabled = true,
            OverridesJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var templateRepository = new InMemoryRepository<PricingRuleTemplate>(null, new PricingRuleTemplate
        {
            TemplateId = templateId,
            Name = "Multiplier template",
            IsActive = true,
            CreatedByAdminId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var parameterRepository = new InMemoryRepository<PricingRuleTemplateParameter>(null, new PricingRuleTemplateParameter
        {
            ParameterId = Guid.NewGuid(),
            TemplateId = templateId,
            ParameterKey = "multiplier",
            DisplayName = "Multiplier",
            DefaultValue = 2m,
            MinValue = 1m,
            MaxValue = 3m,
            IsAdjustable = false,
            CreatedAt = DateTime.UtcNow
        });

        var calendarRepository = new InMemoryApartmentPriceCalendarRepository(new ApartmentPriceCalendar
        {
            PriceId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            PricingPolicyId = templateId,
            VersionId = applicationId,
            VersionNumber = 1,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 3),
            FixedPricePerNight = 200m,
            PriceType = "pricing_policy:multiplier",
            MinNights = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, new ApartmentPriceCalendar
        {
            PriceId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            PricingPolicyId = templateId,
            VersionId = Guid.NewGuid(),
            VersionNumber = 1,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 3),
            FixedPricePerNight = 999m,
            PriceType = "pricing_policy:weekend_multiplier",
            MinNights = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var migrationService = new PricingPolicyMigrationService(
            new NoOpPricingPolicyService(),
            applicationRepository,
            templateRepository,
            parameterRepository,
            apartmentRepository,
            calendarRepository,
            new NoOpHolidayService());

        var service = new ApartmentService(
            apartmentRepository,
            new NoOpTenantWishlistRepository(),
            new NoOpAmenityRepository(),
            new NoOpImageService(),
            new NoOpApartmentMediumService(),
            new NoOpMapper(),
            new NoOpIdentityVerificationService(),
            new NoOpUserRepository(),
            new NoOpRepository<Notification>(),
            new NoOpRepository<PropertyInspection>(),
            calendarRepository,
            new NoOpHolidayService(),
            new NoOpNearbyAttractionRepository(),
            migrationService);

        apartment.BasePricePerNight = 150m;

        await service.UpdateAsync(apartment);

        var regeneratedRows = calendarRepository.Items
            .Where(row => row.VersionId == applicationId)
            .OrderBy(row => row.StartDate)
            .ToList();

        Assert.Equal(3, regeneratedRows.Count);
        Assert.All(regeneratedRows, row => Assert.Equal(300m, row.FixedPricePerNight));
        Assert.DoesNotContain(calendarRepository.Items, row => row.VersionId != applicationId && row.PriceType != null && row.PriceType.StartsWith("pricing_policy"));
    }

    private sealed class TrackedApartmentRepository : IApartmentRepository
    {
        private Apartment _tracked;
        private Apartment _snapshot;

        public TrackedApartmentRepository(Apartment apartment)
        {
            _tracked = apartment;
            _snapshot = Clone(apartment);
        }

        public Task AddAsync(Apartment entity)
        {
            _tracked = entity;
            _snapshot = Clone(entity);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Apartment>> FindAsync(Expression<Func<Apartment, bool>> predicate)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IEnumerable<Apartment>>(compiled(_tracked) ? new[] { _tracked } : Array.Empty<Apartment>());
        }

        public Task<IEnumerable<Apartment>> FindNoTrackingAsync(Expression<Func<Apartment, bool>> predicate)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IEnumerable<Apartment>>(compiled(_snapshot) ? new[] { Clone(_snapshot) } : Array.Empty<Apartment>());
        }

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null, DateOnly? checkInDate = null, DateOnly? checkOutDate = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public Task<Apartment?> GetByIdAsync(Guid id) => Task.FromResult(id == _tracked.ApartmentId ? _tracked : null);

        public Task<Apartment?> GetApartmentWithDetailsAsync(Guid id) => GetByIdAsync(id);

        public Task UpdateListingStatusAsync(Guid apartmentId, string status, string bookingStatus)
        {
            if (_tracked.ApartmentId == apartmentId)
            {
                _tracked.Status = status;
                _tracked.BookingStatus = bookingStatus;
                _snapshot.Status = status;
                _snapshot.BookingStatus = bookingStatus;
            }

            return Task.CompletedTask;
        }

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, IEnumerable<string>? allowedColumns = null, Dictionary<string, string>? filters = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(int page, int pageSize, Guid landlordId, string? sortBy = null, string? sortOrder = null, string? search = null, IEnumerable<string>? allowedColumns = null, Dictionary<string, string>? filters = null)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetApartmentByLandlordIdAsync(Guid landlordId, int page, int pageSize, string? sortBy, string? sortOrder, string? search, Dictionary<string, string>? filters, string[] allowedColumns)
            => Task.FromResult((Enumerable.Empty<Apartment>(), 0));

        public void Remove(Apartment entity) { }

        public void Update(Apartment entity)
        {
            _tracked = entity;
            _snapshot = Clone(entity);
        }

        public Task<int> SaveChangesAsync() => Task.FromResult(1);

        private static Apartment Clone(Apartment source)
        {
            return new Apartment
            {
                ApartmentId = source.ApartmentId,
                LandlordId = source.LandlordId,
                Title = source.Title,
                City = source.City,
                Status = source.Status,
                BookingStatus = source.BookingStatus,
                BasePricePerNight = source.BasePricePerNight,
                Location = source.Location == null ? null : new Point(source.Location.X, source.Location.Y) { SRID = source.Location.SRID }
            };
        }
    }

    private sealed class InMemoryApartmentPriceCalendarRepository : IApartmentPriceCalendarRepository
    {
        private readonly List<ApartmentPriceCalendar> _items;

        public InMemoryApartmentPriceCalendarRepository(params ApartmentPriceCalendar[] items)
        {
            _items = items.ToList();
        }

        public List<ApartmentPriceCalendar> Items => _items;

        public Task AddAsync(ApartmentPriceCalendar entity)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public void AddRange(IEnumerable<ApartmentPriceCalendar> newRecords) => _items.AddRange(newRecords);

        public Task<IEnumerable<ApartmentPriceCalendar>> FindAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(_items.Where(compiled));
        }

        public Task<IEnumerable<ApartmentPriceCalendar>> FindNoTrackingAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate) => FindAsync(predicate);

        public Task<(IEnumerable<ApartmentPriceCalendar> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
            => Task.FromResult((Enumerable.Empty<ApartmentPriceCalendar>(), 0));

        public Task<ApartmentPriceCalendar?> GetByIdAsync(Guid id) => Task.FromResult(_items.FirstOrDefault(item => item.PriceId == id));

        public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingManualRecords(Guid apartmentId, DateOnly start, DateOnly end)
            => Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)_items.Where(item => item.ApartmentId == apartmentId).ToList());

        public Task<IEnumerable<ApartmentPriceCalendar>> GetExistingPriceCalendarRecordsAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
            => Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(_items.Where(item => item.ApartmentId == apartmentId));

        public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingStandardRecords(Guid apartmentId, DateOnly start, DateOnly end)
            => Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)new List<ApartmentPriceCalendar>().AsReadOnly());

        public Task<IEnumerable<ApartmentPriceCalendar>> GetPriceCalendarRecordsForDeletionAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
            => Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(_items.Where(item => item.ApartmentId == apartmentId));

        public void Remove(ApartmentPriceCalendar entity) => _items.RemoveAll(item => item.PriceId == entity.PriceId);

        public void DeleteRange(IEnumerable<ApartmentPriceCalendar> recordsToDelete)
        {
            foreach (var record in recordsToDelete.ToList())
            {
                Remove(record);
            }
        }

        public void Update(ApartmentPriceCalendar entity)
        {
            Remove(entity);
            _items.Add(entity);
        }

        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpPricingPolicyService : IPricingPolicyService
    {
        public Task<IEnumerable<PricingRuleTemplateResponseDto>> GetTemplatesAsync() => Task.FromResult<IEnumerable<PricingRuleTemplateResponseDto>>(Array.Empty<PricingRuleTemplateResponseDto>());
        public Task<PricingRuleTemplateResponseDto> CreateTemplateAsync(CreatePricingRuleTemplateDto dto, Guid adminId) => throw new NotImplementedException();
        public Task<PricingRuleTemplateResponseDto> UpdateTemplateAsync(Guid templateId, CreatePricingRuleTemplateDto dto, Guid adminId) => throw new NotImplementedException();
        public Task<PricingRuleTemplateResponseDto> SetTemplateStatusAsync(Guid templateId, bool isActive, Guid adminId) => throw new NotImplementedException();
        public Task<ApartmentPricingPolicyApplicationResponseDto> ApplyTemplateAsync(Guid apartmentId, CreateApartmentPricingPolicyApplicationDto dto, Guid landlordId) => throw new NotImplementedException();
        public Task<ApartmentPricingPolicyApplicationResponseDto> SetApplicationStatusAsync(Guid apartmentId, Guid applicationId, bool isEnabled, Guid landlordId) => throw new NotImplementedException();
        public Task<ApartmentPricingPolicyApplicationResponseDto> UpdateApplicationOverridesAsync(Guid apartmentId, Guid applicationId, UpdateApartmentPricingPolicyApplicationOverridesDto dto, Guid landlordId) => throw new NotImplementedException();
        public Task<AvailableTemplatesForApartmentDto> GetAvailableTemplatesForApartmentAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate) => throw new NotImplementedException();
        public Task<AvailableTemplatesForApartmentDto> GetAppliedTemplatesForApartmentAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate) => throw new NotImplementedException();
    }

    private sealed class NoOpHolidayService : IHolidayService
    {
        public Task<bool> IsHolidayAsync(DateOnly date, string? locationScope = null) => Task.FromResult(false);
    }

    private sealed class NoOpTenantWishlistRepository : ITenantWishlistRepository
    {
        public Task AddAsync(TenantWishlist entity) => Task.CompletedTask;
        public Task<IEnumerable<TenantWishlist>> FindAsync(Expression<Func<TenantWishlist, bool>> predicate) => Task.FromResult<IEnumerable<TenantWishlist>>(Array.Empty<TenantWishlist>());
        public Task<IEnumerable<TenantWishlist>> FindNoTrackingAsync(Expression<Func<TenantWishlist, bool>> predicate) => Task.FromResult<IEnumerable<TenantWishlist>>(Array.Empty<TenantWishlist>());
        public Task<(IEnumerable<TenantWishlist> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<TenantWishlist>(), 0));
        public Task<TenantWishlist?> GetByIdAsync(Guid id) => Task.FromResult<TenantWishlist?>(null);
        public void Remove(TenantWishlist entity) { }
        public void Update(TenantWishlist entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
        public Task<(IEnumerable<TenantWishlist> Items, int TotalCount)> GetTenantWishlistAsync(Guid tenantId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, decimal? priceMin = null, decimal? priceMax = null, Guid? collectionId = null, Dictionary<string, string>? filters = null) => Task.FromResult((Enumerable.Empty<TenantWishlist>(), 0));
        public Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid collectionId) => Task.FromResult(false);
        public Task<IEnumerable<TenantWishlist>> GetByTenantAndApartmentIdsAsync(Guid tenantId, IEnumerable<Guid> apartmentIds) => Task.FromResult<IEnumerable<TenantWishlist>>(Array.Empty<TenantWishlist>());
        public Task<int> GetWishlistCountAsync(Guid tenantId) => Task.FromResult(0);
        public Task<TenantWishlist?> FindByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId, Guid collectionId) => Task.FromResult<TenantWishlist?>(null);
        public Task<IEnumerable<TenantWishlist>> FindAllByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId) => Task.FromResult<IEnumerable<TenantWishlist>>(Array.Empty<TenantWishlist>());
        public Task<IEnumerable<TenantWishlist>> GetFavoritesOnlyAsync(Guid tenantId, Guid? collectionId = null) => Task.FromResult<IEnumerable<TenantWishlist>>(Array.Empty<TenantWishlist>());
    }

    private sealed class NoOpAmenityRepository : IAmenityRepository
    {
        public Task AddAsync(Amenity entity) => Task.CompletedTask;
        public Task<IEnumerable<Amenity>> FindAsync(Expression<Func<Amenity, bool>> predicate) => Task.FromResult<IEnumerable<Amenity>>(Array.Empty<Amenity>());
        public Task<IEnumerable<Amenity>> FindNoTrackingAsync(Expression<Func<Amenity, bool>> predicate) => Task.FromResult<IEnumerable<Amenity>>(Array.Empty<Amenity>());
        public Task<(IEnumerable<Amenity> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<Amenity>(), 0));
        public Task<Amenity?> GetByIdAsync(Guid id) => Task.FromResult<Amenity?>(null);
        public void Remove(Amenity entity) { }
        public void Update(Amenity entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpImageService : IImageService
    {
        public Task<string> UploadImageAsync(IFormFile file) => Task.FromResult(string.Empty);
    }

    private sealed class NoOpApartmentMediumService : IApartmentMediumService
    {
        public Task<ApartmentMedium?> GetByIdAsync(Guid id) => Task.FromResult<ApartmentMedium?>(null);
        public Task<(IEnumerable<ApartmentMedium> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<ApartmentMedium>(), 0));
        public Task<ApartmentMedium> CreateAsync(ApartmentMedium entity) => Task.FromResult(entity);
        public Task UpdateAsync(ApartmentMedium entity) => Task.CompletedTask;
        public Task DeleteAsync(Guid id) => Task.CompletedTask;
    }

    private sealed class NoOpIdentityVerificationService : IIdentityVerificationService
    {
        public Task EnsureUserVerifiedForBookingAsync(Guid userId) => Task.CompletedTask;
        public Task EnsureUserVerifiedForInspectionAsync(Guid landlordId) => Task.CompletedTask;
        public Task EnsureUserVerifiedForListingSubmissionAsync(Guid landlordId) => Task.CompletedTask;
        public Task<Guid[]> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto) => Task.FromResult(Array.Empty<Guid>());
        public Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto) => Task.CompletedTask;
        public Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetUserDocumentsAsync(Guid userId, int page, int pageSize, string? sortBy = null, string? sortOrder = null) => Task.FromResult((Enumerable.Empty<IdentityDocumentDto>(), 0));
        public Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetAllDocumentsAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null) => Task.FromResult((Enumerable.Empty<IdentityDocumentDto>(), 0));
    }

    private sealed class NoOpUserRepository : IUserRepository
    {
        public Task AddAsync(User entity) => Task.CompletedTask;
        public Task<IEnumerable<User>> FindAsync(Expression<Func<User, bool>> predicate) => Task.FromResult<IEnumerable<User>>(Array.Empty<User>());
        public Task<IEnumerable<User>> FindNoTrackingAsync(Expression<Func<User, bool>> predicate) => Task.FromResult<IEnumerable<User>>(Array.Empty<User>());
        public Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<User>(), 0));
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult<User?>(null);
        public void Remove(User entity) { }
        public void Update(User entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpRepository<T> : IRepository<T> where T : class
    {
        public Task AddAsync(T entity) => Task.CompletedTask;
        public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<IEnumerable<T>>(Array.Empty<T>());
        public Task<IEnumerable<T>> FindNoTrackingAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<IEnumerable<T>>(Array.Empty<T>());
        public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<T>(), 0));
        public Task<T?> GetByIdAsync(Guid id) => Task.FromResult<T?>(null);
        public void Remove(T entity) { }
        public void Update(T entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpNearbyAttractionRepository : INearbyAttractionRepository
    {
        public Task AddAsync(NearbyAttraction entity) => Task.CompletedTask;
        public Task<IEnumerable<NearbyAttraction>> FindAsync(Expression<Func<NearbyAttraction, bool>> predicate) => Task.FromResult<IEnumerable<NearbyAttraction>>(Array.Empty<NearbyAttraction>());
        public Task<IEnumerable<NearbyAttraction>> FindNoTrackingAsync(Expression<Func<NearbyAttraction, bool>> predicate) => Task.FromResult<IEnumerable<NearbyAttraction>>(Array.Empty<NearbyAttraction>());
        public Task<(IEnumerable<NearbyAttraction> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<NearbyAttraction>(), 0));
        public Task<NearbyAttraction?> GetByIdAsync(Guid id) => Task.FromResult<NearbyAttraction?>(null);
        public void Remove(NearbyAttraction entity) { }
        public void Update(NearbyAttraction entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpMapper : IMapper
    {
        public TDestination Map<TDestination>(object source) => throw new NotImplementedException();
        public TDestination Map<TSource, TDestination>(TSource source) => throw new NotImplementedException();
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) => throw new NotImplementedException();
        public TDestination Map<TDestination>(object source, Action<IMappingOperationOptions<object, TDestination>> opts) => throw new NotImplementedException();
        public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts) => throw new NotImplementedException();
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts) => throw new NotImplementedException();
        public object Map(object source, Type sourceType, Type destinationType) => throw new NotImplementedException();
        public object Map(object source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts) => throw new NotImplementedException();
        public object Map(object source, object destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts) => throw new NotImplementedException();
        public object Map(object source, object destination, Type sourceType, Type destinationType) => throw new NotImplementedException();
        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object parameters, params Expression<Func<TDestination, object>>[] membersToExpand) => throw new NotImplementedException();
        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand) => throw new NotImplementedException();
        public IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object> parameters, params string[] membersToExpand) => throw new NotImplementedException();
        public IConfigurationProvider ConfigurationProvider => throw new NotImplementedException();
        public IMapper CreateMapper() => this;
    }
}