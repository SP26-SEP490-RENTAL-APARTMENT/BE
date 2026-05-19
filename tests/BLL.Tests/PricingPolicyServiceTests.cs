using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Xunit;

namespace BLL.Tests
{
    public class PricingPolicyServiceTests
    {
        [Fact]
        public async Task ApplyTemplate_CreatesCalendarRow_WithExpectedPrice()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var landlordId = Guid.NewGuid();
            var apartmentId = Guid.NewGuid();

            var apartment = new Apartment
            {
                ApartmentId = apartmentId,
                LandlordId = landlordId,
                BasePricePerNight = 100m
            };

            var template = new PricingRuleTemplate
            {
                TemplateId = Guid.NewGuid(),
                CreatedByAdminId = adminId,
                Name = "Multiplier template",
                IsActive = true
            };

            var parameter = new PricingRuleTemplateParameter
            {
                ParameterId = Guid.NewGuid(),
                TemplateId = template.TemplateId,
                ParameterKey = "multiplier",
                DisplayName = "Multiplier",
                DefaultValue = 1.2m,
                MinValue = 1.0m,
                MaxValue = 2.0m,
                IsAdjustable = true
            };

            var templatesRepo = new InMemoryRepo<PricingRuleTemplate>(new[] { template });
            var paramsRepo = new InMemoryRepo<PricingRuleTemplateParameter>(new[] { parameter });
            var applicationsRepo = new InMemoryRepo<ApartmentPricingPolicyApplication>();
            var apartmentRepo = new InMemoryRepo<Apartment>(new[] { apartment });
            var calendarRepo = new InMemoryRepo<ApartmentPriceCalendar>();

            var holidayService = new TestHolidayService();

            var service = new PricingPolicyService(
                templatesRepo,
                paramsRepo,
                applicationsRepo,
                apartmentRepo as DAL.Repository.Interfaces.IApartmentRepository ?? new FakeApartmentRepository(apartmentRepo),
                calendarRepo as IApartmentPriceCalendarRepository ?? new FakeCalendarRepository(calendarRepo),
                holidayService
            );


            var dto = new CreateApartmentPricingPolicyApplicationDto
            {
                TemplateId = template.TemplateId,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
                IsEnabled = true,
                Overrides = new Dictionary<string, decimal> { { "multiplier", 1.5m } }
            };

            // Act
            var result = await service.ApplyTemplateAsync(apartmentId, dto, landlordId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(apartmentId, result.ApartmentId);
            Assert.Equal(template.TemplateId, result.TemplateId);
            Assert.True(result.IsEnabled);
            Assert.Equal(1.5m, result.EffectiveMultiplier);
            Assert.Equal(150m, result.EffectivePricePerNight);

            // Verify calendar row created
            var generated = calendarRepo.Items.OfType<ApartmentPriceCalendar>().FirstOrDefault(r => r.VersionId == result.ApplicationId);
            Assert.NotNull(generated);
            Assert.Equal(150m, generated.FixedPricePerNight);
            Assert.Equal("pricing_policy", generated.PriceType);
        }

        [Fact]
        public async Task ApplyTemplate_CreatesDailyRows_WithWeekendMultiplierChanges()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var landlordId = Guid.NewGuid();
            var apartmentId = Guid.NewGuid();

            var apartment = new Apartment
            {
                ApartmentId = apartmentId,
                LandlordId = landlordId,
                BasePricePerNight = 100m
            };

            var template = new PricingRuleTemplate
            {
                TemplateId = Guid.NewGuid(),
                CreatedByAdminId = adminId,
                Name = "Multiplier template",
                IsActive = true
            };

            var parameters = new[]
            {
                new PricingRuleTemplateParameter
                {
                    ParameterId = Guid.NewGuid(),
                    TemplateId = template.TemplateId,
                    ParameterKey = "multiplier",
                    DisplayName = "Multiplier",
                    DefaultValue = 1.1m,
                    MinValue = 1.0m,
                    MaxValue = 2.0m,
                    IsAdjustable = true
                },
                new PricingRuleTemplateParameter
                {
                    ParameterId = Guid.NewGuid(),
                    TemplateId = template.TemplateId,
                    ParameterKey = "weekend_multiplier",
                    DisplayName = "Weekend Multiplier",
                    DefaultValue = 1.5m,
                    MinValue = 1.0m,
                    MaxValue = 3.0m,
                    IsAdjustable = true
                }
            };

            var templatesRepo = new InMemoryRepo<PricingRuleTemplate>(new[] { template });
            var paramsRepo = new InMemoryRepo<PricingRuleTemplateParameter>(parameters);
            var applicationsRepo = new InMemoryRepo<ApartmentPricingPolicyApplication>();
            var apartmentRepo = new InMemoryRepo<Apartment>(new[] { apartment });
            var calendarRepo = new InMemoryRepo<ApartmentPriceCalendar>();
            var holidayService = new TestHolidayService();

            var service = new PricingPolicyService(
                templatesRepo,
                paramsRepo,
                applicationsRepo,
                apartmentRepo as DAL.Repository.Interfaces.IApartmentRepository ?? new FakeApartmentRepository(apartmentRepo),
                calendarRepo as IApartmentPriceCalendarRepository ?? new FakeCalendarRepository(calendarRepo),
                holidayService
            );

            var dto = new CreateApartmentPricingPolicyApplicationDto
            {
                TemplateId = template.TemplateId,
                StartDate = new DateOnly(2026, 5, 22),
                EndDate = new DateOnly(2026, 5, 24),
                IsEnabled = true,
                Overrides = new Dictionary<string, decimal>()
            };

            // Act
            var result = await service.ApplyTemplateAsync(apartmentId, dto, landlordId);

            // Assert
            Assert.NotNull(result);

            var generatedRows = calendarRepo.Items
                .OfType<ApartmentPriceCalendar>()
                .Where(row => row.VersionId == result.ApplicationId)
                .OrderBy(row => row.StartDate)
                .ToList();

            Assert.Equal(3, generatedRows.Count);
            Assert.Equal(new DateOnly(2026, 5, 22), generatedRows[0].StartDate);
            Assert.Equal(new DateOnly(2026, 5, 22), generatedRows[0].EndDate);
            Assert.Equal(110m, generatedRows[0].FixedPricePerNight);

            Assert.Equal(new DateOnly(2026, 5, 23), generatedRows[1].StartDate);
            Assert.Equal(new DateOnly(2026, 5, 23), generatedRows[1].EndDate);
            Assert.Equal(150m, generatedRows[1].FixedPricePerNight);

            Assert.Equal(new DateOnly(2026, 5, 24), generatedRows[2].StartDate);
            Assert.Equal(new DateOnly(2026, 5, 24), generatedRows[2].EndDate);
            Assert.Equal(150m, generatedRows[2].FixedPricePerNight);
        }

        [Fact]
        public async Task UpdateApplicationOverrides_ChangesMultiplier_WithoutDates()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var landlordId = Guid.NewGuid();
            var apartmentId = Guid.NewGuid();

            var apartment = new Apartment
            {
                ApartmentId = apartmentId,
                LandlordId = landlordId,
                BasePricePerNight = 100m
            };

            var template = new PricingRuleTemplate
            {
                TemplateId = Guid.NewGuid(),
                CreatedByAdminId = adminId,
                Name = "Multiplier template",
                IsActive = true
            };

            var parameter = new PricingRuleTemplateParameter
            {
                ParameterId = Guid.NewGuid(),
                TemplateId = template.TemplateId,
                ParameterKey = "multiplier",
                DisplayName = "Multiplier",
                DefaultValue = 1.2m,
                MinValue = 1.0m,
                MaxValue = 2.0m,
                IsAdjustable = true
            };

            var templatesRepo = new InMemoryRepo<PricingRuleTemplate>(new[] { template });
            var paramsRepo = new InMemoryRepo<PricingRuleTemplateParameter>(new[] { parameter });
            var applicationsRepo = new InMemoryRepo<ApartmentPricingPolicyApplication>();
            var apartmentRepo = new InMemoryRepo<Apartment>(new[] { apartment });
            var calendarRepo = new InMemoryRepo<ApartmentPriceCalendar>();
            var holidayService = new TestHolidayService();

            var service = new PricingPolicyService(
                templatesRepo,
                paramsRepo,
                applicationsRepo,
                apartmentRepo as DAL.Repository.Interfaces.IApartmentRepository ?? new FakeApartmentRepository(apartmentRepo),
                calendarRepo as IApartmentPriceCalendarRepository ?? new FakeCalendarRepository(calendarRepo),
                holidayService
            );

            var applyDto = new CreateApartmentPricingPolicyApplicationDto
            {
                TemplateId = template.TemplateId,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
                IsEnabled = true,
                Overrides = new Dictionary<string, decimal> { { "multiplier", 1.2m } }
            };

            var applied = await service.ApplyTemplateAsync(apartmentId, applyDto, landlordId);

            var updateDto = new UpdateApartmentPricingPolicyApplicationOverridesDto
            {
                Overrides = new Dictionary<string, decimal> { { "multiplier", 1.6m } }
            };

            // Act
            var updated = await service.UpdateApplicationOverridesAsync(apartmentId, applied.ApplicationId, updateDto, landlordId);

            // Assert
            Assert.Equal(1.6m, updated.EffectiveMultiplier);
            Assert.Equal(160m, updated.EffectivePricePerNight);

            var generated = calendarRepo.Items.OfType<ApartmentPriceCalendar>().FirstOrDefault(r => r.VersionId == applied.ApplicationId);
            Assert.NotNull(generated);
            Assert.Equal(160m, generated.FixedPricePerNight);
        }

        // Lightweight in-memory repository for tests
        private class InMemoryRepo<T> : IRepository<T> where T : class
        {
            public List<T> Items { get; } = new List<T>();

            public InMemoryRepo() { }
            public InMemoryRepo(IEnumerable<T> items) { Items.AddRange(items); }

            public Task AddAsync(T entity)
            {
                Items.Add(entity);
                return Task.CompletedTask;
            }

            public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
            {
                var compiled = predicate.Compile();
                return Task.FromResult(Items.Where(compiled));
            }

            public Task<T?> GetByIdAsync(Guid id)
            {
                var prop = typeof(T).GetProperties().FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
                if (prop == null) return Task.FromResult<T?>(null);
                var item = Items.FirstOrDefault(i => {
                    var val = prop.GetValue(i);
                    return val is Guid g && g == id;
                });
                return Task.FromResult(item);
            }

            public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
                => Task.FromResult(((IEnumerable<T>)Items, Items.Count));

            public void Remove(T entity) => Items.Remove(entity);

            public void Update(T entity)
            {
                // no-op for tests
            }

            public Task<int> SaveChangesAsync() => Task.FromResult(0);
        }

        // Fake wrappers to satisfy specific repository interfaces used in the service
        private class FakeApartmentRepository : DAL.Repository.Interfaces.IApartmentRepository
        {
            private readonly InMemoryRepo<Apartment> _repo;
            public FakeApartmentRepository(InMemoryRepo<Apartment> repo) { _repo = repo; }
            public Task AddAsync(Apartment entity) => _repo.AddAsync(entity);
            public Task<IEnumerable<Apartment>> FindAsync(Expression<Func<Apartment, bool>> predicate) => _repo.FindAsync(predicate);
            public Task<Apartment?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
            public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => _repo.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, allowedColumns);
            public void Remove(Apartment entity) => _repo.Remove(entity);
            public void Update(Apartment entity) => _repo.Update(entity);
            public Task<int> SaveChangesAsync() => _repo.SaveChangesAsync();

            // Additional interface members (not used in this test) - provide minimal implementations or throw
            public Task<Apartment?> GetApartmentWithDetailsAsync(Guid id) => GetByIdAsync(id);
            public Task UpdateListingStatusAsync(Guid apartmentId, string status, string bookingStatus) => throw new NotImplementedException();
            public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null, DateOnly? checkInDate = null, DateOnly? checkOutDate = null) => throw new NotImplementedException();
            public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, IEnumerable<string>? allowedColumns = null, Dictionary<string, string>? filters = null) => throw new NotImplementedException();
            public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(int page, int pageSize, Guid landlordId, string? sortBy = null, string? sortOrder = null, string? search = null, IEnumerable<string>? allowedColumns = null, Dictionary<string, string>? filters = null) => throw new NotImplementedException();
            public Task<(IEnumerable<Apartment> Items, int TotalCount)> GetApartmentByLandlordIdAsync(Guid landlordId, int page, int pageSize, string? sortBy, string? sortOrder, string? search, Dictionary<string, string>? filters, string[] allowedColumns) => throw new NotImplementedException();
        }

        private class FakeCalendarRepository : IApartmentPriceCalendarRepository
        {
            private readonly InMemoryRepo<ApartmentPriceCalendar> _repo;
            public FakeCalendarRepository(InMemoryRepo<ApartmentPriceCalendar> repo) { _repo = repo; }
            public void AddRange(IEnumerable<ApartmentPriceCalendar> newRecords) { _repo.Items.AddRange(newRecords); }
            public void DeleteRange(IEnumerable<ApartmentPriceCalendar> recordsToDelete) { foreach (var r in recordsToDelete) _repo.Remove(r); }
            public Task<IEnumerable<ApartmentPriceCalendar>> GetExistingPriceCalendarRecordsAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate) => Task.FromResult(_repo.Items.AsEnumerable());
            public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingManualRecords(Guid apartmentId, DateOnly start, DateOnly end) => Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)_repo.Items.ToList());
            public Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingStandardRecords(Guid apartmentId, DateOnly start, DateOnly end) => Task.FromResult((IReadOnlyList<ApartmentPriceCalendar>)_repo.Items.ToList());
            public Task<IEnumerable<ApartmentPriceCalendar>> GetPriceCalendarRecordsForDeletionAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate) => Task.FromResult(_repo.Items.AsEnumerable());
            public Task AddAsync(ApartmentPriceCalendar entity) => _repo.AddAsync(entity);
            public Task<IEnumerable<ApartmentPriceCalendar>> FindAsync(Expression<Func<ApartmentPriceCalendar, bool>> predicate) => _repo.FindAsync(predicate);
            public Task<ApartmentPriceCalendar?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
            public Task<(IEnumerable<ApartmentPriceCalendar> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null) => _repo.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, allowedColumns);
            public void Remove(ApartmentPriceCalendar entity) => _repo.Remove(entity);
            public void Update(ApartmentPriceCalendar entity) => _repo.Update(entity);
            public Task<int> SaveChangesAsync() => _repo.SaveChangesAsync();
        }

        private class TestHolidayService : IHolidayService
        {
            public Task<bool> IsHolidayAsync(DateOnly date, string? locationScope = null) => Task.FromResult(false);
        }
    }
}
