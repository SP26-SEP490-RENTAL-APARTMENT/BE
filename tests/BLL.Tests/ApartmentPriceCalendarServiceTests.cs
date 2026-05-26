using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace BLL.Tests;

public class ApartmentPriceCalendarServiceTests
{
    [Fact]
    public async Task GetResolvedCalendarAsync_UsesRecordPriceTypeAsSource_WhenManualListContainsNonManualRecord()
    {
        var apartmentId = Guid.NewGuid();
        var start = new DateOnly(2026, 5, 1);
        var end = new DateOnly(2026, 5, 31);

        var manualRecord = new ApartmentPriceCalendar
        {
            PriceId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            StartDate = start,
            EndDate = end,
            FixedPricePerNight = 200m,
            PriceType = "seasonal",
            CreatedAt = DateTime.UtcNow
        };

        var repo = new InMemoryApartmentPriceCalendarRepository(new[] { manualRecord });
        var db = CreateDbContext(apartmentId, 135m);
        var auth = new NoOpAuthService();
        var cache = new NoOpCacheService();

        var service = new ApartmentPriceCalendarService(repo, auth, cache, db);

        var resolutions = (await service.GetResolvedCalendarAsync(apartmentId, start, end)).ToList();

        // Pick a day inside the range
        var mid = resolutions.First(r => r.Date == new DateOnly(2026, 5, 10));
        Assert.Equal(200m, mid.FinalPricePerNight);
        Assert.Equal("seasonal", mid.Source);
    }

    [Fact]
    public async Task GetResolvedCalendarAsync_FallsBackToBaseRate_WhenManualRecordHasNoFixedPrice()
    {
        var apartmentId = Guid.NewGuid();
        var start = new DateOnly(2026, 5, 11);
        var end = new DateOnly(2026, 5, 16);

        var manualRecord = new ApartmentPriceCalendar
        {
            PriceId = Guid.NewGuid(),
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 31),
            PriceType = "seasonal",
            CreatedAt = DateTime.UtcNow
        };

        var repo = new InMemoryApartmentPriceCalendarRepository(new[] { manualRecord });
        var db = CreateDbContext(apartmentId, 135m);
        var auth = new NoOpAuthService();
        var cache = new NoOpCacheService();

        var service = new ApartmentPriceCalendarService(repo, auth, cache, db);

        var resolutions = (await service.GetResolvedCalendarAsync(apartmentId, start, end)).ToList();

        Assert.All(resolutions, item =>
        {
            Assert.Equal(135m, item.FinalPricePerNight);
            Assert.Equal("BaseRate", item.Source);
        });
    }

    private static AppDbContext CreateDbContext(Guid apartmentId, decimal basePricePerNight)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new AppDbContext(options);
        db.Apartments.Add(new Apartment
        {
            ApartmentId = apartmentId,
            LandlordId = Guid.NewGuid(),
            Title = "Test Apartment",
            City = "Hanoi",
            Location = new Point(0, 0),
            BasePricePerNight = basePricePerNight,
            Status = "posted",
            BookingStatus = "available",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        return db;
    }

    private sealed class InMemoryApartmentPriceCalendarRepository : IApartmentPriceCalendarRepository
    {
        private readonly List<ApartmentPriceCalendar> _items;

        public InMemoryApartmentPriceCalendarRepository(IEnumerable<ApartmentPriceCalendar> items)
        {
            _items = items.ToList();
        }

        public void AddRange(IEnumerable<ApartmentPriceCalendar> newRecords) => _items.AddRange(newRecords);
        public void DeleteRange(IEnumerable<ApartmentPriceCalendar> recordsToDelete)
        {
            foreach (var r in recordsToDelete) _items.RemoveAll(x => x.PriceId == r.PriceId);
        }

        public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingManualRecords(Guid apartmentId, DateOnly start, DateOnly end)
        {
            var result = _items.Where(r => r.ApartmentId == apartmentId && r.StartDate <= end && r.EndDate >= start).ToList().AsReadOnly();
            return Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)result);
        }

        public Task<IEnumerable<ApartmentPriceCalendar>> GetExistingPriceCalendarRecordsAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
        {
            var result = _items.Where(r => r.ApartmentId == apartmentId && r.StartDate <= endDate && r.EndDate >= startDate);
            return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(result);
        }

        public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingStandardRecords(Guid apartmentId, DateOnly start, DateOnly end)
        {
            // For this test, standard records are empty
            return Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)new List<ApartmentPriceCalendar>().AsReadOnly());
        }

        public Task<IEnumerable<ApartmentPriceCalendar>> GetPriceCalendarRecordsForDeletionAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
        {
            var result = _items.Where(r => r.ApartmentId == apartmentId && r.StartDate <= endDate && r.EndDate >= startDate);
            return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(result);
        }

        // IRepository members (minimal implementations)
        public Task AddAsync(ApartmentPriceCalendar entity)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ApartmentPriceCalendar>> FindAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate)
        {
            var compiled = predicate.Compile();
            var result = _items.Where(compiled);
            return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(result);
        }

        public Task<(IEnumerable<ApartmentPriceCalendar> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
        {
            return Task.FromResult((Enumerable.Empty<ApartmentPriceCalendar>(), 0));
        }

        public Task<ApartmentPriceCalendar?> GetByIdAsync(Guid id) => Task.FromResult(_items.FirstOrDefault(x => x.PriceId == id));
        public void Remove(ApartmentPriceCalendar entity) => _items.RemoveAll(x => x.PriceId == entity.PriceId);
        public void Update(ApartmentPriceCalendar entity)
        {
            Remove(entity);
            _items.Add(entity);
        }

        public Task<int> SaveChangesAsync() => Task.FromResult(1);

        public Task<IEnumerable<ApartmentPriceCalendar>> FindNoTrackingAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate)
        {
            var compiled = predicate.Compile();
            var result = _items.Where(compiled);
            return Task.FromResult<IEnumerable<ApartmentPriceCalendar>>(result);
        }
    }

    private sealed class NoOpAuthService : IAuthService
    {
        public Task<AddUserRoleResponseDto?> AddRoleAsync(Guid userId, AddUserRoleRequestDto dto) => throw new NotImplementedException();
        public Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto) => throw new NotImplementedException();
        public Task<LoginResponseDto?> RegisterAsync(RegisterRequestDto dto) => throw new NotImplementedException();
        public Task<ResponseDTO> RequestVerificationAsync(RequestVerificationDto dto) => throw new NotImplementedException();
        public Task<RefreshTokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto dto) => throw new NotImplementedException();
        public Task<ResponseDTO> ResetPasswordAsync(ResetPasswordWithVerificationDto dto) => throw new NotImplementedException();
        public Task<ResponseDTO> RequestPasswordResetAsync(PasswordResetRequestDto dto) => throw new NotImplementedException();
        public Task<ResponseDTO> ResetPasswordByTokenAsync(string token, ResetPasswordByTokenDto dto) => throw new NotImplementedException();
        public Task<ResponseDTO> ChangePasswordAsync(Guid userId, PasswordResetDto dto) => throw new NotImplementedException();
        public Task<bool> LogoutAsync(RefreshTokenRequestDto dto) => throw new NotImplementedException();
        public Task<bool> IsUserOwnerOrManager(Guid userId, Guid apartmentId) => Task.FromResult(true);
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan expiry) => Task.CompletedTask;
    }
}
