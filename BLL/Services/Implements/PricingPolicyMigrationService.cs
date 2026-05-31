using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

/// <summary>
/// Service to migrate existing pricing policy calendar rows to include parameter key information.
/// This ensures that old calendar rows generated before the parameter key update will display
/// the correct multiplier type (multiplier, weekend_multiplier, or holiday_multiplier).
/// </summary>
public sealed class PricingPolicyMigrationService
{
    private const string PolicyPriceType = "pricing_policy";
    private readonly IPricingPolicyService _pricingPolicyService;
    private readonly IRepository<ApartmentPricingPolicyApplication> _applicationRepository;
    private readonly IRepository<PricingRuleTemplate> _templateRepository;
    private readonly IRepository<PricingRuleTemplateParameter> _parameterRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IApartmentPriceCalendarRepository _calendarRepository;
    private readonly IHolidayService _holidayService;

    public PricingPolicyMigrationService(
        IPricingPolicyService pricingPolicyService,
        IRepository<ApartmentPricingPolicyApplication> applicationRepository,
        IRepository<PricingRuleTemplate> templateRepository,
        IRepository<PricingRuleTemplateParameter> parameterRepository,
        IApartmentRepository apartmentRepository,
        IApartmentPriceCalendarRepository calendarRepository,
        IHolidayService holidayService)
    {
        _pricingPolicyService = pricingPolicyService;
        _applicationRepository = applicationRepository;
        _templateRepository = templateRepository;
        _parameterRepository = parameterRepository;
        _apartmentRepository = apartmentRepository;
        _calendarRepository = calendarRepository;
        _holidayService = holidayService;
    }

    /// <summary>
    /// Regenerates all pricing policy calendar rows to include parameter key information.
    /// This migrates old calendar rows (with PriceType = "pricing_policy") to the new format
    /// (with PriceType = "pricing_policy:multiplier", etc.).
    /// </summary>
    public async Task<MigrationResult> MigrateAllPricingPoliciesAsync()
    {
        var applications = (await _applicationRepository.FindAsync(a => a.IsEnabled == true)).ToList();
        var result = new MigrationResult
        {
            TotalApplications = applications.Count,
            SuccessfullyMigrated = 0,
            Failed = 0,
            Errors = new List<string>()
        };

        foreach (var application in applications)
        {
            try
            {
                await RegeneratePricingPolicyAsync(application);
                result.SuccessfullyMigrated++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Application {application.ApplicationId}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Regenerates all enabled pricing policy calendar rows for a specific apartment.
    /// </summary>
    public async Task RegeneratePricingPoliciesForApartmentAsync(Guid apartmentId)
    {
        var applications = (await _applicationRepository.FindAsync(a =>
            a.ApartmentId == apartmentId &&
            a.IsEnabled == true)).ToList();

        foreach (var application in applications)
        {
            await RegeneratePricingPolicyAsync(application);
        }
    }

    /// <summary>
    /// Regenerates calendar rows for a specific pricing policy application.
    /// </summary>
    public async Task RegeneratePricingPolicyAsync(ApartmentPricingPolicyApplication application)
    {
        var apartment = await _apartmentRepository.GetByIdAsync(application.ApartmentId)
            ?? throw new ArgumentException($"Apartment {application.ApartmentId} not found.");

        var template = await _templateRepository.GetByIdAsync(application.TemplateId)
            ?? throw new ArgumentException($"Template {application.TemplateId} not found.");

        var parameters = (await _parameterRepository.FindAsync(p => p.TemplateId == template.TemplateId)).ToList();

        // Remove all overlapping calendar rows for the apartment before regenerating.
        var recordsToDelete = await _calendarRepository.GetPriceCalendarRecordsForDeletionAsync(
            application.ApartmentId,
            application.StartDate,
            application.EndDate);

        if (recordsToDelete.Any())
        {
            _calendarRepository.DeleteRange(recordsToDelete);
            await _calendarRepository.SaveChangesAsync();
        }

        // Regenerate with parameter keys
        var cursor = application.StartDate;
        while (cursor <= application.EndDate)
        {
            var usedParameterKey = await ResolveUsedParameterKeyAsync(parameters, cursor);
            var dailyMultiplier = await ResolveMultiplierAsync(parameters, cursor);
            var price = Math.Round(apartment.BasePricePerNight * dailyMultiplier, 2, MidpointRounding.AwayFromZero);
            var priceTypeWithParam = $"{PolicyPriceType}:{usedParameterKey}";

            await _calendarRepository.AddAsync(new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                PricingPolicyId = template.TemplateId,
                VersionId = application.ApplicationId,
                VersionNumber = 1,
                StartDate = cursor,
                EndDate = cursor,
                FixedPricePerNight = price,
                PriceType = priceTypeWithParam,
                DiscountPercentage = null,
                IsDiscount = false,
                MinNights = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            cursor = cursor.AddDays(1);
        }

        await _calendarRepository.SaveChangesAsync();
    }

    private async Task<string> ResolveUsedParameterKeyAsync(
        IReadOnlyCollection<PricingRuleTemplateParameter> parameters,
        DateOnly date)
    {
        decimal GetParameterValue(string key, decimal fallback)
        {
            var p = parameters.FirstOrDefault(x => 
                string.Equals(x.ParameterKey, key, StringComparison.OrdinalIgnoreCase));
            return p != null ? p.DefaultValue : fallback;
        }

        var holidayKey = "holiday_multiplier";
        var weekendKey = "weekend_multiplier";
        var baseKey = "multiplier";

        // Holiday has priority
        var holidayMultiplier = GetParameterValue(holidayKey, decimal.MinValue);
        if (holidayMultiplier != decimal.MinValue && await _holidayService.IsHolidayAsync(date))
            return holidayKey;

        // Weekend
        var weekendMultiplier = GetParameterValue(weekendKey, decimal.MinValue);
        if (weekendMultiplier != decimal.MinValue)
        {
            var dow = date.DayOfWeek;
            if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday)
                return weekendKey;
        }

        // fallback to generic multiplier
        return baseKey;
    }

    private async Task<decimal> ResolveMultiplierAsync(
        IReadOnlyCollection<PricingRuleTemplateParameter> parameters,
        DateOnly date)
    {
        decimal GetParameterValue(string key, decimal fallback)
        {
            var p = parameters.FirstOrDefault(x => 
                string.Equals(x.ParameterKey, key, StringComparison.OrdinalIgnoreCase));
            return p != null ? p.DefaultValue : fallback;
        }

        var holidayKey = "holiday_multiplier";
        var weekendKey = "weekend_multiplier";
        var baseKey = "multiplier";

        // Holiday has priority
        var holidayMultiplier = GetParameterValue(holidayKey, decimal.MinValue);
        if (holidayMultiplier != decimal.MinValue && await _holidayService.IsHolidayAsync(date))
            return holidayMultiplier;

        // Weekend
        var weekendMultiplier = GetParameterValue(weekendKey, decimal.MinValue);
        if (weekendMultiplier != decimal.MinValue)
        {
            var dow = date.DayOfWeek;
            if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday)
                return weekendMultiplier;
        }

        // fallback to generic multiplier
        var generic = GetParameterValue(baseKey, 1.0m);
        return generic == decimal.MinValue ? 1.0m : generic;
    }
}

public class MigrationResult
{
    public int TotalApplications { get; set; }
    public int SuccessfullyMigrated { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}
