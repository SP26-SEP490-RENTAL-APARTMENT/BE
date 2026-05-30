using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using BLL.Mappings;
using Common.DTOs;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;

namespace BLL.Tests;

public class ApartmentServicePublicPriceChangesTests
{
    [Fact]
    public async Task GetAllPublicResponseAsync_PopulatesPriceChangesForCalendarRanges()
    {
        var apartmentId = Guid.NewGuid();
        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Test Apartment",
            BasePricePerNight = 100m,
            City = "Hanoi",
            Location = new Point(0, 0)
        };

        var apartmentRepository = new InMemoryApartmentRepository(apartment);
        var calendarRepository = new InMemoryApartmentPriceCalendarRepository(new[]
        {
            new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = new DateOnly(2026, 5, 1),
                EndDate = new DateOnly(2026, 5, 7),
                FixedPricePerNight = 120m,
                PriceType = "manual_override",
                CreatedAt = DateTime.UtcNow
            },
            new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 15),
                DiscountPercentage = 10m,
                IsDiscount = true,
                PriceType = "low_season",
                CreatedAt = DateTime.UtcNow
            }
        });

        var service = CreateService(apartmentRepository, calendarRepository);

        var (items, totalCount) = await service.GetAllPublicResponseAsync(1, 10);
        var response = items.Single();

        Assert.Equal(1, totalCount);
        Assert.Equal(2, response.PriceChanges.Count);
        Assert.Equal(100m, response.PriceChanges[0].OldPricePerNight);
        Assert.Equal(120m, response.PriceChanges[0].NewPricePerNight);
        Assert.Equal("manual override", response.PriceChanges[0].Reason);
        Assert.Equal(new DateOnly(2026, 5, 1), response.PriceChanges[0].StartDate);
        Assert.Equal(new DateOnly(2026, 5, 7), response.PriceChanges[0].EndDate);

        Assert.Equal(120m, response.PriceChanges[1].OldPricePerNight);
        Assert.Equal(90m, response.PriceChanges[1].NewPricePerNight);
        Assert.Equal("low season", response.PriceChanges[1].Reason);
        Assert.Equal(new DateOnly(2026, 6, 1), response.PriceChanges[1].StartDate);
        Assert.Equal(new DateOnly(2026, 6, 15), response.PriceChanges[1].EndDate);
    }

    [Fact]
    public async Task GetAllPublicResponseAsync_ConsolidatesConsecutivePricePeriodsWithSamePrice()
    {
        var apartmentId = Guid.NewGuid();
        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Test Apartment",
            BasePricePerNight = 100m,
            City = "Hanoi",
            Location = new Point(0, 0)
        };

        var apartmentRepository = new InMemoryApartmentRepository(apartment);
        // Simulate multiple calendar records for May with same price (e.g., May 1-1, May 2-7, May 8-31)
        // All should consolidate into a single price change from May 1-31 with the LATEST reason
        var calendarRepository = new InMemoryApartmentPriceCalendarRepository(new[]
        {
            new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = new DateOnly(2026, 5, 1),
                EndDate = new DateOnly(2026, 5, 1),
                FixedPricePerNight = 135m,
                PriceType = "manual_override",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = new DateOnly(2026, 5, 2),
                EndDate = new DateOnly(2026, 5, 31),
                FixedPricePerNight = 135m,
                PriceType = "low_season",  // Different type but same price
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        });

        var service = CreateService(apartmentRepository, calendarRepository);

        var (items, totalCount) = await service.GetAllPublicResponseAsync(1, 10);
        var response = items.Single();

        Assert.Equal(1, totalCount);
        // Should consolidate into a single price change covering May 1-31 at 135
        Assert.Single(response.PriceChanges);
        Assert.Equal(100m, response.PriceChanges[0].OldPricePerNight);
        Assert.Equal(135m, response.PriceChanges[0].NewPricePerNight);
        // Reason should reflect the LATEST calendar record's type
        Assert.Equal("low season", response.PriceChanges[0].Reason);
        Assert.Equal(new DateOnly(2026, 5, 1), response.PriceChanges[0].StartDate);
        Assert.Equal(new DateOnly(2026, 5, 31), response.PriceChanges[0].EndDate);
    }

    [Fact]
    public async Task GetApartmentWithDetailsResponseAsync_GroupsNearbyAttractionsByTier()
    {
        var apartmentId = Guid.NewGuid();
        var apartment = new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Test Apartment",
            BasePricePerNight = 100m,
            City = "Hanoi",
            Latitude = 21.028511m,
            Longitude = 105.804817m,
            Location = new Point(105.804817, 21.028511) { SRID = 4326 }
        };

        var apartmentRepository = new InMemoryApartmentRepository(apartment);
        var attractionRepository = new InMemoryNearbyAttractionRepository(new[]
        {
            new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = "Old Quarter",
                NameVi = "Phố cổ",
                Type = "landmark",
                City = "Hanoi",
                Address = "Hoan Kiem",
                Location = new Point(105.804900, 21.031000) { SRID = 4326 }
            },
            new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = "History Museum",
                NameVi = "Bảo tàng lịch sử",
                Type = "museum",
                City = "Hanoi",
                Address = "Dong Da",
                Location = new Point(105.807000, 21.040000) { SRID = 4326 }
            },
            new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = "City Park",
                NameVi = "Công viên thành phố",
                Type = "park",
                City = "Hanoi",
                Address = "Ba Dinh",
                Location = new Point(105.798000, 21.033500) { SRID = 4326 }
            },
            new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = "Night Market",
                NameVi = "Chợ đêm",
                Type = "shopping",
                City = "Hanoi",
                Address = "Old Quarter",
                Location = new Point(105.804817, 21.060000) { SRID = 4326 }
            },
            new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = "Sky Viewpoint",
                NameVi = "Điểm ngắm cảnh",
                Type = "landmark",
                City = "Hanoi",
                Address = "West Lake",
                Location = new Point(105.804817, 21.071000) { SRID = 4326 }
            },
            new NearbyAttraction
            {
                AttractionId = Guid.NewGuid(),
                NameEn = "Metro Station",
                NameVi = "Ga tàu điện",
                Type = "transport",
                City = "Hanoi",
                Address = "Far Away",
                Location = new Point(105.804817, 21.100000) { SRID = 4326 }
            }
        });

        var service = CreateService(apartmentRepository, new InMemoryApartmentPriceCalendarRepository(Array.Empty<ApartmentPriceCalendar>()), attractionRepository);

        var response = await service.GetApartmentWithDetailsResponseAsync(apartmentId, includeExpandedNearbyAttractions: true);

        Assert.NotNull(response);
        Assert.Equal(3, response!.NearbyAttractions.PrimaryAttractions.Count);
        Assert.Equal(2, response.NearbyAttractions.ExpandedAttractions.Count);
        Assert.True(response.NearbyAttractions.HasExpandedAttractions);
        Assert.Equal("Old Quarter", response.NearbyAttractions.PrimaryAttractions[0].NameEn);
        Assert.Equal("History Museum", response.NearbyAttractions.PrimaryAttractions[1].NameEn);
        Assert.Equal("City Park", response.NearbyAttractions.PrimaryAttractions[2].NameEn);
        Assert.Equal("Night Market", response.NearbyAttractions.ExpandedAttractions[0].NameEn);
        Assert.Equal("Sky Viewpoint", response.NearbyAttractions.ExpandedAttractions[1].NameEn);
        Assert.InRange(response.NearbyAttractions.PrimaryAttractions[0].DistanceKm, 0d, 3d);
        Assert.InRange(response.NearbyAttractions.ExpandedAttractions[0].DistanceKm, 3d, 5d);
    }

    private static ApartmentService CreateService(
        IApartmentRepository apartmentRepository,
        IApartmentPriceCalendarRepository calendarRepository,
        INearbyAttractionRepository? nearbyAttractionRepository = null)
    {
        var mapper = new SimpleMapper();

        return new ApartmentService(
            apartmentRepository,
            new NoOpTenantWishlistRepository(),
            new NoOpAmenityRepository(),
            new NoOpImageService(),
            new NoOpApartmentMediumService(),
            mapper,
            new NoOpIdentityVerificationService(),
            new NoOpUserRepository(),
            new NoOpRepository<Notification>(),
            new NoOpRepository<PropertyInspection>(),
                calendarRepository,
                new NoOpHolidayService(),
                nearbyAttractionRepository ?? new NoOpNearbyAttractionRepository());
    }

    private sealed class NoOpIdentityVerificationService : IIdentityVerificationService
    {
        public Task EnsureUserVerifiedForBookingAsync(Guid userId) => Task.CompletedTask;

        public Task EnsureUserVerifiedForInspectionAsync(Guid landlordId) => Task.CompletedTask;

        public Task EnsureUserVerifiedForListingSubmissionAsync(Guid landlordId) => Task.CompletedTask;

        public Task<Guid[]> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto)
            => Task.FromResult(Array.Empty<Guid>());

        public Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto) => Task.CompletedTask;

        public Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetUserDocumentsAsync(
            Guid userId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null)
            => Task.FromResult((Items: Enumerable.Empty<IdentityDocumentDto>(), TotalCount: 0));

        public Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetAllDocumentsAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null)
            => Task.FromResult((Items: Enumerable.Empty<IdentityDocumentDto>(), TotalCount: 0));
    }

    private sealed class SimpleMapper : IMapper
    {
        public TDestination Map<TDestination>(object source)
        {
            if (source is IEnumerable<Apartment> apartments && typeof(TDestination) == typeof(List<ApartmentResponseDto>))
            {
                var list = apartments.Select(a => new ApartmentResponseDto
                {
                    ApartmentId = a.ApartmentId,
                    Title = a.Title,
                    BasePricePerNight = a.BasePricePerNight,
                    City = a.City,
                    Latitude = a.Latitude,
                    Longitude = a.Longitude,
                }).ToList();

                return (TDestination)(object)list;
            }

            if (source is Apartment ap && typeof(TDestination) == typeof(ApartmentResponseDto))
            {
                var dto = new ApartmentResponseDto
                {
                    ApartmentId = ap.ApartmentId,
                    Title = ap.Title,
                    BasePricePerNight = ap.BasePricePerNight,
                    City = ap.City,
                    Latitude = ap.Latitude,
                    Longitude = ap.Longitude,
                };

                return (TDestination)(object)dto;
            }

            throw new NotImplementedException("Map source/destination not implemented in SimpleMapper.");
        }

        // The rest of the IMapper interface is not used by the test; provide basic stubs.
        public object Map(object source, Type sourceType, Type destinationType) => Map<object>(source);
        public TDestination Map<TSource, TDestination>(TSource source) => Map<TDestination>(source!);
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            return Map<TDestination>(source!);
        }

        public TDestination Map<TDestination>(object source, Action<IMappingOperationOptions<object, TDestination>> opts)
        {
            return Map<TDestination>(source);
        }

        public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts)
        {
            return Map<TDestination>(source!);
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts)
        {
            return Map<TDestination>(source!);
        }

        public object Map(object source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts) => Map<object>(source);

        public object Map(object source, object destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
        {
            throw new NotImplementedException();
        }

        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object parameters, params System.Linq.Expressions.Expression<Func<TDestination, object>>[] membersToExpand)
        {
            throw new NotImplementedException();
        }

        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand)
        {
            throw new NotImplementedException();
        }

        public IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object> parameters, params string[] membersToExpand)
        {
            throw new NotImplementedException();
        }

        public object Map(object source, object destination, Type sourceType, Type destinationType)
        {
            throw new NotImplementedException();
        }

        public IConfigurationProvider ConfigurationProvider => throw new NotImplementedException();
        public IMapper CreateMapper() => this;
    }

    private sealed class NoOpTenantWishlistRepository : ITenantWishlistRepository
    {
        public Task AddAsync(TenantWishlist entity) => Task.CompletedTask;
        public Task<IEnumerable<TenantWishlist>> FindAsync(Expression<Func<TenantWishlist, bool>> predicate) => Task.FromResult<IEnumerable<TenantWishlist>>(Enumerable.Empty<TenantWishlist>());
        public Task<IEnumerable<TenantWishlist>> FindNoTrackingAsync(Expression<Func<TenantWishlist, bool>> predicate) => Task.FromResult<IEnumerable<TenantWishlist>>(Enumerable.Empty<TenantWishlist>());
        public Task<(IEnumerable<TenantWishlist> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<TenantWishlist>(), 0));
        public Task<TenantWishlist?> GetByIdAsync(Guid id) => Task.FromResult<TenantWishlist?>(null);
        public void Remove(TenantWishlist entity) { }
        public void Update(TenantWishlist entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
        public Task<(IEnumerable<TenantWishlist> Items, int TotalCount)> GetTenantWishlistAsync(Guid tenantId, int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, decimal? priceMin = null, decimal? priceMax = null, Guid? collectionId = null, Dictionary<string, string>? filters = null) => Task.FromResult((Enumerable.Empty<TenantWishlist>(), 0));
        public Task<bool> IsApartmentInWishlistAsync(Guid tenantId, Guid apartmentId, Guid collectionId) => Task.FromResult(false);
        public Task<IEnumerable<TenantWishlist>> GetByTenantAndApartmentIdsAsync(Guid tenantId, IEnumerable<Guid> apartmentIds) => Task.FromResult<IEnumerable<TenantWishlist>>(Enumerable.Empty<TenantWishlist>());
        public Task<int> GetWishlistCountAsync(Guid tenantId) => Task.FromResult(0);
        public Task<TenantWishlist?> FindByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId, Guid collectionId) => Task.FromResult<TenantWishlist?>(null);
        public Task<IEnumerable<TenantWishlist>> FindAllByTenantAndApartmentAsync(Guid tenantId, Guid apartmentId) => Task.FromResult<IEnumerable<TenantWishlist>>(Enumerable.Empty<TenantWishlist>());
        public Task<IEnumerable<TenantWishlist>> GetFavoritesOnlyAsync(Guid tenantId, Guid? collectionId = null) => Task.FromResult<IEnumerable<TenantWishlist>>(Enumerable.Empty<TenantWishlist>());
    }

    private sealed class NoOpAmenityRepository : IAmenityRepository
    {
        public Task AddAsync(Amenity entity) => Task.CompletedTask;
        public Task<IEnumerable<Amenity>> FindAsync(Expression<Func<Amenity, bool>> predicate) => Task.FromResult<IEnumerable<Amenity>>(Enumerable.Empty<Amenity>());
        public Task<IEnumerable<Amenity>> FindNoTrackingAsync(Expression<Func<Amenity, bool>> predicate) => Task.FromResult<IEnumerable<Amenity>>(Enumerable.Empty<Amenity>());
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

    private sealed class NoOpHolidayService : IHolidayService
    {
        public Task<bool> IsHolidayAsync(DateOnly date, string? locationScope = null) => Task.FromResult(false);
    }

    private sealed class InMemoryNearbyAttractionRepository : INearbyAttractionRepository
    {
        private readonly List<NearbyAttraction> _attractions;

        public InMemoryNearbyAttractionRepository()
        {
            _attractions = new List<NearbyAttraction>();
        }

        public InMemoryNearbyAttractionRepository(IEnumerable<NearbyAttraction> attractions)
        {
            _attractions = attractions.ToList();
        }

        public Task AddAsync(NearbyAttraction entity)
        {
            _attractions.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<NearbyAttraction>> FindAsync(Expression<Func<NearbyAttraction, bool>> predicate)
        {
            var compiled = predicate.Compile();
            IEnumerable<NearbyAttraction> result = _attractions.Where(compiled);
            return Task.FromResult(result);
        }

        public Task<IEnumerable<NearbyAttraction>> FindNoTrackingAsync(Expression<Func<NearbyAttraction, bool>> predicate)
        {
            var compiled = predicate.Compile();
            IEnumerable<NearbyAttraction> result = _attractions.Where(compiled);
            return Task.FromResult(result);
        }

        public Task<(IEnumerable<NearbyAttraction> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            return Task.FromResult((_attractions.AsEnumerable(), _attractions.Count));
        }

        public Task<NearbyAttraction?> GetByIdAsync(Guid id)
        {
            NearbyAttraction? result = _attractions.FirstOrDefault(a => a.AttractionId == id);
            return Task.FromResult(result);
        }

        public void Remove(NearbyAttraction entity)
        {
            _attractions.Remove(entity);
        }

        public void Update(NearbyAttraction entity)
        {
        }

        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpNearbyAttractionRepository : INearbyAttractionRepository
    {
        public Task AddAsync(NearbyAttraction entity) => Task.CompletedTask;
        public Task<IEnumerable<NearbyAttraction>> FindAsync(Expression<Func<NearbyAttraction, bool>> predicate) => Task.FromResult<IEnumerable<NearbyAttraction>>(Enumerable.Empty<NearbyAttraction>());
        public Task<IEnumerable<NearbyAttraction>> FindNoTrackingAsync(Expression<Func<NearbyAttraction, bool>> predicate) => Task.FromResult<IEnumerable<NearbyAttraction>>(Enumerable.Empty<NearbyAttraction>());
        public Task<(IEnumerable<NearbyAttraction> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<NearbyAttraction>(), 0));
        public Task<NearbyAttraction?> GetByIdAsync(Guid id) => Task.FromResult<NearbyAttraction?>(null);
        public void Remove(NearbyAttraction entity) { }
        public void Update(NearbyAttraction entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpUserRepository : IUserRepository
    {
        public Task AddAsync(User entity) => Task.CompletedTask;
        public Task<IEnumerable<User>> FindAsync(Expression<Func<User, bool>> predicate) => Task.FromResult<IEnumerable<User>>(Enumerable.Empty<User>());
        public Task<IEnumerable<User>> FindNoTrackingAsync(Expression<Func<User, bool>> predicate) => Task.FromResult<IEnumerable<User>>(Enumerable.Empty<User>());
        public Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<User>(), 0));
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult<User?>(null);
        public void Remove(User entity) { }
        public void Update(User entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }

    private sealed class NoOpRepository<T> : IRepository<T> where T : class
    {
        public Task AddAsync(T entity) => Task.CompletedTask;
        public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<IEnumerable<T>>(Enumerable.Empty<T>());
        public Task<IEnumerable<T>> FindNoTrackingAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<IEnumerable<T>>(Enumerable.Empty<T>());
        public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => Task.FromResult((Enumerable.Empty<T>(), 0));
        public Task<T?> GetByIdAsync(Guid id) => Task.FromResult<T?>(null);
        public void Remove(T entity) { }
        public void Update(T entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }
}