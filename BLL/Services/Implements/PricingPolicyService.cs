using System.Text.Json;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PricingPolicyService : IPricingPolicyService
{
    private const string PolicyPriceType = "pricing_policy";
    private const string MultiplierKey = "multiplier";

    private readonly IRepository<PricingRuleTemplate> _templateRepository;
    private readonly IRepository<PricingRuleTemplateParameter> _parameterRepository;
    private readonly IRepository<ApartmentPricingPolicyApplication> _applicationRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IApartmentPriceCalendarRepository _calendarRepository;
    private readonly IHolidayService _holidayService;

    public PricingPolicyService(
        IRepository<PricingRuleTemplate> templateRepository,
        IRepository<PricingRuleTemplateParameter> parameterRepository,
        IRepository<ApartmentPricingPolicyApplication> applicationRepository,
        IApartmentRepository apartmentRepository,
        IApartmentPriceCalendarRepository calendarRepository,
        IHolidayService holidayService)
    {
        _templateRepository = templateRepository;
        _parameterRepository = parameterRepository;
        _applicationRepository = applicationRepository;
        _apartmentRepository = apartmentRepository;
        _calendarRepository = calendarRepository;
        _holidayService = holidayService;
    }

    public async Task<AvailableTemplatesForApartmentDto> GetAvailableTemplatesForApartmentAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
    {
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId)
            ?? throw new ArgumentException("Apartment not found.");

        var templates = (await _templateRepository.FindAsync(t => t.IsActive)).ToList();
        var templateIds = templates.Select(t => t.TemplateId).ToArray();
        var parameters = templateIds.Length == 0
            ? new List<PricingRuleTemplateParameter>()
            : (await _parameterRepository.FindAsync(p => templateIds.Contains(p.TemplateId))).ToList();

        var result = new AvailableTemplatesForApartmentDto
        {
            ApartmentId = apartment.ApartmentId,
            ApartmentBasePrice = apartment.BasePricePerNight
        };

        foreach (var template in templates)
        {
            var parms = parameters.Where(p => p.TemplateId == template.TemplateId).ToList();
            var defaults = parms.ToDictionary(p => p.ParameterKey, p => p.DefaultValue, StringComparer.OrdinalIgnoreCase);
            var multiplier = await ResolveMultiplierAsync(parms, defaults, startDate);
            var previewPrice = Math.Round(apartment.BasePricePerNight * multiplier, 2, MidpointRounding.AwayFromZero);

            result.Templates.Add(new TemplatePreviewDto
            {
                TemplateId = template.TemplateId,
                Name = template.Name,
                Description = template.Description,
                IsActive = template.IsActive,
                Parameters = parms.Select(p => new PricingRuleTemplateParameterResponseDto
                {
                    ParameterId = p.ParameterId,
                    TemplateId = p.TemplateId,
                    ParameterKey = p.ParameterKey,
                    DisplayName = p.DisplayName,
                    DefaultValue = p.DefaultValue,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue,
                    IsAdjustable = p.IsAdjustable
                }).ToList(),
                StartDate = startDate,
                EndDate = endDate,
                PreviewMultiplier = multiplier,
                PreviewPricePerNight = previewPrice
            });
        }

        return result;
    }

    public async Task<AvailableTemplatesForApartmentDto> GetAppliedTemplatesForApartmentAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate)
    {
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId)
            ?? throw new ArgumentException("Apartment not found.");

        var applications = (await _applicationRepository.FindAsync(a =>
            a.ApartmentId == apartmentId &&
            a.StartDate <= endDate &&
            a.EndDate >= startDate &&
            a.IsEnabled)).ToList();

        var templateIds = applications.Select(a => a.TemplateId).Distinct().ToArray();
        var templates = templateIds.Length == 0
            ? new List<PricingRuleTemplate>()
            : (await _templateRepository.FindAsync(t => templateIds.Contains(t.TemplateId))).ToList();

        var parameters = templateIds.Length == 0
            ? new List<PricingRuleTemplateParameter>()
            : (await _parameterRepository.FindAsync(p => templateIds.Contains(p.TemplateId))).ToList();

        var result = new AvailableTemplatesForApartmentDto
        {
            ApartmentId = apartment.ApartmentId,
            ApartmentBasePrice = apartment.BasePricePerNight
        };

        foreach (var application in applications.OrderBy(a => a.StartDate))
        {
            var template = templates.FirstOrDefault(t => t.TemplateId == application.TemplateId);
            if (template == null) continue;

            var parms = parameters.Where(p => p.TemplateId == template.TemplateId).ToList();
            var overrides = DeserializeOverrides(application.OverridesJson);
            var multiplier = await ResolveMultiplierAsync(parms, overrides, application.StartDate);
            var previewPrice = Math.Round(apartment.BasePricePerNight * multiplier, 2, MidpointRounding.AwayFromZero);

            result.Templates.Add(new TemplatePreviewDto
            {
                ApplicationId = application.ApplicationId,
                TemplateId = template.TemplateId,
                Name = template.Name,
                Description = template.Description,
                IsActive = template.IsActive,
                Parameters = parms.Select(p => new PricingRuleTemplateParameterResponseDto
                {
                    ParameterId = p.ParameterId,
                    TemplateId = p.TemplateId,
                    ParameterKey = p.ParameterKey,
                    DisplayName = p.DisplayName,
                    DefaultValue = p.DefaultValue,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue,
                    IsAdjustable = p.IsAdjustable
                }).ToList(),
                StartDate = application.StartDate,
                EndDate = application.EndDate,
                PreviewMultiplier = multiplier,
                PreviewPricePerNight = previewPrice
            });
        }

        return result;
    }

    public async Task<IEnumerable<PricingRuleTemplateResponseDto>> GetTemplatesAsync()
    {
        var templates = (await _templateRepository.FindAsync(_ => true)).ToList();
        var parameters = templates.Count == 0
            ? new List<PricingRuleTemplateParameter>()
            : (await _parameterRepository.FindAsync(p => templates.Select(t => t.TemplateId).Contains(p.TemplateId))).ToList();

        return templates
            .OrderBy(t => t.Name)
            .Select(template => MapTemplate(template, parameters.Where(p => p.TemplateId == template.TemplateId)));
    }

    public async Task<PricingRuleTemplateResponseDto> CreateTemplateAsync(CreatePricingRuleTemplateDto dto, Guid adminId)
    {
        ValidateTemplateDto(dto);

        var template = new PricingRuleTemplate
        {
            TemplateId = Guid.NewGuid(),
            CreatedByAdminId = adminId,
            Name = dto.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _templateRepository.AddAsync(template);

        foreach (var parameterDto in dto.Parameters)
        {
            await _parameterRepository.AddAsync(new PricingRuleTemplateParameter
            {
                ParameterId = Guid.NewGuid(),
                TemplateId = template.TemplateId,
                ParameterKey = parameterDto.ParameterKey.Trim(),
                DisplayName = parameterDto.DisplayName.Trim(),
                DefaultValue = parameterDto.DefaultValue,
                MinValue = parameterDto.MinValue,
                MaxValue = parameterDto.MaxValue,
                IsAdjustable = parameterDto.IsAdjustable,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _templateRepository.SaveChangesAsync();
        await _parameterRepository.SaveChangesAsync();

        return await GetTemplateResponseAsync(template.TemplateId)
            ?? throw new InvalidOperationException("Failed to create pricing template.");
    }

    public async Task<PricingRuleTemplateResponseDto> UpdateTemplateAsync(Guid templateId, CreatePricingRuleTemplateDto dto, Guid adminId)
    {
        ValidateTemplateDto(dto);

        var template = await _templateRepository.GetByIdAsync(templateId)
            ?? throw new ArgumentException("Pricing template not found.");

        template.Name = dto.Name.Trim();
        template.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        template.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        template.IsActive = dto.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.CreatedByAdminId = adminId;

        var existingParameters = (await _parameterRepository.FindAsync(p => p.TemplateId == templateId)).ToList();
        var existingByKey = existingParameters
            .GroupBy(p => p.ParameterKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameterDto in dto.Parameters)
        {
            var key = parameterDto.ParameterKey.Trim();
            incomingKeys.Add(key);

            if (existingByKey.TryGetValue(key, out var existingParameter))
            {
                existingParameter.DisplayName = parameterDto.DisplayName.Trim();
                existingParameter.DefaultValue = parameterDto.DefaultValue;
                existingParameter.MinValue = parameterDto.MinValue;
                existingParameter.MaxValue = parameterDto.MaxValue;
                existingParameter.IsAdjustable = parameterDto.IsAdjustable;
                _parameterRepository.Update(existingParameter);
                continue;
            }

            await _parameterRepository.AddAsync(new PricingRuleTemplateParameter
            {
                ParameterId = Guid.NewGuid(),
                TemplateId = template.TemplateId,
                ParameterKey = key,
                DisplayName = parameterDto.DisplayName.Trim(),
                DefaultValue = parameterDto.DefaultValue,
                MinValue = parameterDto.MinValue,
                MaxValue = parameterDto.MaxValue,
                IsAdjustable = parameterDto.IsAdjustable,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var staleParameter in existingParameters.Where(p => !incomingKeys.Contains(p.ParameterKey)).ToList())
        {
            _parameterRepository.Remove(staleParameter);
        }

        _templateRepository.Update(template);
        await _templateRepository.SaveChangesAsync();
        await _parameterRepository.SaveChangesAsync();

        return await GetTemplateResponseAsync(template.TemplateId)
            ?? throw new InvalidOperationException("Failed to update pricing template.");
    }

    public async Task<PricingRuleTemplateResponseDto> SetTemplateStatusAsync(Guid templateId, bool isActive, Guid adminId)
    {
        var template = await _templateRepository.GetByIdAsync(templateId)
            ?? throw new ArgumentException("Pricing template not found.");

        template.IsActive = isActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.CreatedByAdminId = adminId;

        _templateRepository.Update(template);
        await _templateRepository.SaveChangesAsync();

        return await GetTemplateResponseAsync(template.TemplateId)
            ?? throw new InvalidOperationException("Failed to update pricing template status.");
    }

    public async Task<ApartmentPricingPolicyApplicationResponseDto> ApplyTemplateAsync(
        Guid apartmentId,
        CreateApartmentPricingPolicyApplicationDto dto,
        Guid landlordId)
    {
        if (dto.StartDate > dto.EndDate)
        {
            throw new ArgumentException("Start date cannot be after end date.");
        }

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId)
            ?? throw new ArgumentException("Apartment not found.");

        if (apartment.LandlordId != landlordId)
        {
            throw new UnauthorizedAccessException("You do not have permission to apply pricing policies for this apartment.");
        }

        var template = await _templateRepository.GetByIdAsync(dto.TemplateId)
            ?? throw new ArgumentException("Pricing template not found.");

        if (!template.IsActive)
        {
            throw new ArgumentException("Pricing template is inactive.");
        }

        var parameters = (await _parameterRepository.FindAsync(p => p.TemplateId == template.TemplateId)).ToList();
        var effectiveOverrides = ValidateOverrides(parameters, dto.Overrides);
        var effectiveMultiplier = await ResolveMultiplierAsync(parameters, effectiveOverrides, dto.StartDate);

        var existingApplications = await _applicationRepository.FindAsync(a =>
            a.ApartmentId == apartmentId &&
            a.TemplateId == template.TemplateId &&
            a.StartDate == dto.StartDate &&
            a.EndDate == dto.EndDate);

        ApartmentPricingPolicyApplication application;
        if (existingApplications.Any())
        {
            application = existingApplications.First();
            await RemoveGeneratedCalendarRowsAsync(application.ApplicationId);
            application.IsEnabled = dto.IsEnabled;
            application.OverridesJson = SerializeOverrides(effectiveOverrides);
            application.UpdatedAt = DateTime.UtcNow;
            _applicationRepository.Update(application);
        }
        else
        {
            application = new ApartmentPricingPolicyApplication
            {
                ApplicationId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                TemplateId = template.TemplateId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsEnabled = dto.IsEnabled,
                OverridesJson = SerializeOverrides(effectiveOverrides),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _applicationRepository.AddAsync(application);
        }

        if (application.IsEnabled)
        {
            await UpsertGeneratedCalendarRowsAsync(apartment, application, template, parameters, effectiveOverrides);
        }

        await _applicationRepository.SaveChangesAsync();

        return await BuildApplicationResponseAsync(application, template, effectiveMultiplier, apartment.BasePricePerNight)
            ?? throw new InvalidOperationException("Failed to apply pricing policy.");
    }

    public async Task<ApartmentPricingPolicyApplicationResponseDto> SetApplicationStatusAsync(
        Guid apartmentId,
        Guid applicationId,
        bool isEnabled,
        Guid landlordId)
    {
        var application = await _applicationRepository.GetByIdAsync(applicationId)
            ?? throw new ArgumentException("Pricing policy application not found.");

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId)
            ?? throw new ArgumentException("Apartment not found.");

        if (application.ApartmentId != apartmentId)
        {
            throw new ArgumentException("Pricing policy application does not belong to this apartment.");
        }

        if (apartment.LandlordId != landlordId)
        {
            throw new UnauthorizedAccessException("You do not have permission to modify pricing policies for this apartment.");
        }

        var template = await _templateRepository.GetByIdAsync(application.TemplateId)
            ?? throw new ArgumentException("Pricing template not found.");
        var parameters = (await _parameterRepository.FindAsync(p => p.TemplateId == template.TemplateId)).ToList();
        var overrides = DeserializeOverrides(application.OverridesJson);
        var effectiveMultiplier = await ResolveMultiplierAsync(parameters, overrides, application.StartDate);

        if (!isEnabled)
        {
            await RemoveGeneratedCalendarRowsAsync(application.ApplicationId);
        }
        else
        {
            await UpsertGeneratedCalendarRowsAsync(apartment, application, template, parameters, overrides);
        }

        application.IsEnabled = isEnabled;
        application.UpdatedAt = DateTime.UtcNow;
        _applicationRepository.Update(application);
        await _applicationRepository.SaveChangesAsync();

        return await BuildApplicationResponseAsync(application, template, effectiveMultiplier, apartment.BasePricePerNight)
            ?? throw new InvalidOperationException("Failed to update pricing policy application.");
    }

    public async Task<ApartmentPricingPolicyApplicationResponseDto> UpdateApplicationOverridesAsync(
        Guid apartmentId,
        Guid applicationId,
        UpdateApartmentPricingPolicyApplicationOverridesDto dto,
        Guid landlordId)
    {
        var application = await _applicationRepository.GetByIdAsync(applicationId)
            ?? throw new ArgumentException("Pricing policy application not found.");

        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId)
            ?? throw new ArgumentException("Apartment not found.");

        if (application.ApartmentId != apartmentId)
        {
            throw new ArgumentException("Pricing policy application does not belong to this apartment.");
        }

        if (apartment.LandlordId != landlordId)
        {
            throw new UnauthorizedAccessException("You do not have permission to modify pricing policies for this apartment.");
        }

        var template = await _templateRepository.GetByIdAsync(application.TemplateId)
            ?? throw new ArgumentException("Pricing template not found.");

        if (!template.IsActive)
        {
            throw new ArgumentException("Pricing template is inactive.");
        }

        var parameters = (await _parameterRepository.FindAsync(p => p.TemplateId == template.TemplateId)).ToList();
        var effectiveOverrides = ValidateOverrides(parameters, dto.Overrides);
        var effectiveMultiplier = await ResolveMultiplierAsync(parameters, effectiveOverrides, application.StartDate);

        application.OverridesJson = SerializeOverrides(effectiveOverrides);
        application.UpdatedAt = DateTime.UtcNow;
        _applicationRepository.Update(application);

        if (application.IsEnabled)
        {
            await UpsertGeneratedCalendarRowsAsync(apartment, application, template, parameters, effectiveOverrides);
        }

        await _applicationRepository.SaveChangesAsync();

        return await BuildApplicationResponseAsync(application, template, effectiveMultiplier, apartment.BasePricePerNight)
            ?? throw new InvalidOperationException("Failed to update pricing policy application overrides.");
    }

    private async Task RemoveGeneratedCalendarRowsAsync(Guid applicationId)
    {
        var generatedRows = await _calendarRepository.FindAsync(row =>
            row.VersionId == applicationId &&
            (row.PriceType == PolicyPriceType || row.PriceType!.StartsWith(PolicyPriceType + ":")));

        foreach (var row in generatedRows.ToList())
        {
            _calendarRepository.Remove(row);
        }

        await _calendarRepository.SaveChangesAsync();
    }

    private async Task UpsertGeneratedCalendarRowsAsync(
        Apartment apartment,
        ApartmentPricingPolicyApplication application,
        PricingRuleTemplate template,
        IReadOnlyCollection<PricingRuleTemplateParameter> parameters,
        IReadOnlyDictionary<string, decimal> overrides)
    {
        await RemoveGeneratedCalendarRowsAsync(application.ApplicationId);

        var cursor = application.StartDate;
        while (cursor <= application.EndDate)
        {
            var dailyMultiplier = await ResolveMultiplierAsync(parameters, overrides, cursor);
            var price = Math.Round(apartment.BasePricePerNight * dailyMultiplier, 2, MidpointRounding.AwayFromZero);
            var usedParameterKey = await ResolveUsedParameterKeyAsync(parameters, overrides, cursor);
            var priceTypeWithParam = string.IsNullOrEmpty(usedParameterKey) 
                ? PolicyPriceType 
                : $"{PolicyPriceType}:{usedParameterKey}";

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
        IReadOnlyDictionary<string, decimal> overrides,
        DateOnly date)
    {
        // lookup keys
        decimal GetOverrideOrDefault(string key, decimal fallback)
        {
            if (overrides != null && overrides.TryGetValue(key, out var v)) return v;
            var p = parameters.FirstOrDefault(x => string.Equals(x.ParameterKey, key, StringComparison.OrdinalIgnoreCase));
            return p != null ? p.DefaultValue : fallback;
        }

        var holidayKey = "holiday_multiplier";
        var weekendKey = "weekend_multiplier";
        var baseKey = MultiplierKey;

        // Holiday has priority
        var holidayMultiplier = GetOverrideOrDefault(holidayKey, decimal.MinValue);
        if (holidayMultiplier != decimal.MinValue)
        {
            if (await _holidayService.IsHolidayAsync(date))
                return holidayKey;
        }

        // Weekend
        var weekendMultiplier = GetOverrideOrDefault(weekendKey, decimal.MinValue);
        if (weekendMultiplier != decimal.MinValue)
        {
            var dow = date.DayOfWeek;
            if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday)
                return weekendKey;
        }

        // fallback to generic multiplier
        return baseKey;
    }

    private static void ValidateTemplateDto(CreatePricingRuleTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException("Template name is required.");
        }

        if (dto.Parameters == null || dto.Parameters.Count == 0)
        {
            throw new ArgumentException("At least one template parameter is required.");
        }

        var duplicateKey = dto.Parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ParameterKey))
            .GroupBy(parameter => parameter.ParameterKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key;

        if (!string.IsNullOrWhiteSpace(duplicateKey))
        {
            throw new ArgumentException($"Template parameter key '{duplicateKey}' is duplicated.");
        }

        foreach (var parameter in dto.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.ParameterKey))
            {
                throw new ArgumentException("Template parameter key is required.");
            }

            if (string.IsNullOrWhiteSpace(parameter.DisplayName))
            {
                throw new ArgumentException("Template parameter display name is required.");
            }

            if (parameter.MinValue.HasValue && parameter.MaxValue.HasValue && parameter.MinValue > parameter.MaxValue)
            {
                throw new ArgumentException($"Parameter '{parameter.ParameterKey}' has an invalid range.");
            }

            if (parameter.DefaultValue < 0)
            {
                throw new ArgumentException($"Parameter '{parameter.ParameterKey}' default value must be non-negative.");
            }
        }
    }

    private static Dictionary<string, decimal> ValidateOverrides(
        IReadOnlyCollection<PricingRuleTemplateParameter> parameters,
        IDictionary<string, decimal> overrides)
    {
        var parameterLookup = parameters.ToDictionary(p => p.ParameterKey, StringComparer.OrdinalIgnoreCase);
        var effectiveOverrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var overrideEntry in overrides)
        {
            if (!parameterLookup.TryGetValue(overrideEntry.Key, out var parameter))
            {
                throw new ArgumentException($"Parameter '{overrideEntry.Key}' does not exist on the template.");
            }

            if (!parameter.IsAdjustable)
            {
                throw new ArgumentException($"Parameter '{overrideEntry.Key}' cannot be adjusted by the landlord.");
            }

            if (parameter.MinValue.HasValue && overrideEntry.Value < parameter.MinValue.Value)
            {
                throw new ArgumentException($"Parameter '{overrideEntry.Key}' must be at least {parameter.MinValue.Value}.");
            }

            if (parameter.MaxValue.HasValue && overrideEntry.Value > parameter.MaxValue.Value)
            {
                throw new ArgumentException($"Parameter '{overrideEntry.Key}' must be at most {parameter.MaxValue.Value}.");
            }

            effectiveOverrides[overrideEntry.Key] = overrideEntry.Value;
        }

        return effectiveOverrides;
    }

    private async Task<decimal> ResolveMultiplierAsync(
        IReadOnlyCollection<PricingRuleTemplateParameter> parameters,
        IReadOnlyDictionary<string, decimal> overrides,
        DateOnly? date = null)
    {
        // lookup keys
        decimal GetOverrideOrDefault(string key, decimal fallback)
        {
            if (overrides != null && overrides.TryGetValue(key, out var v)) return v;
            var p = parameters.FirstOrDefault(x => string.Equals(x.ParameterKey, key, StringComparison.OrdinalIgnoreCase));
            return p != null ? p.DefaultValue : fallback;
        }

        var holidayKey = "holiday_multiplier";
        var weekendKey = "weekend_multiplier";
        var baseKey = MultiplierKey;

        if (date.HasValue)
        {
            // Holiday has priority
            var holidayMultiplier = GetOverrideOrDefault(holidayKey, decimal.MinValue);
            if (holidayMultiplier != decimal.MinValue)
            {
                if (await _holidayService.IsHolidayAsync(date.Value))
                    return holidayMultiplier;
            }

            // Weekend
            var weekendMultiplier = GetOverrideOrDefault(weekendKey, decimal.MinValue);
            if (weekendMultiplier != decimal.MinValue)
            {
                var dow = date.Value.DayOfWeek;
                if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday)
                    return weekendMultiplier;
            }
        }

        // fallback to generic multiplier
        var generic = GetOverrideOrDefault(baseKey, 1.0m);
        if (generic == decimal.MinValue) return 1.0m;
        return generic;
    }

    private static string SerializeOverrides(Dictionary<string, decimal> overrides)
        => overrides.Count == 0 ? "{}" : JsonSerializer.Serialize(overrides);

    private static Dictionary<string, decimal> DeserializeOverrides(string? overridesJson)
    {
        if (string.IsNullOrWhiteSpace(overridesJson))
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, decimal>>(overridesJson)
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PricingRuleTemplateResponseDto?> GetTemplateResponseAsync(Guid templateId)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
        {
            return null;
        }

        var parameters = await _parameterRepository.FindAsync(p => p.TemplateId == templateId);
        return MapTemplate(template, parameters);
    }

    private static PricingRuleTemplateResponseDto MapTemplate(
        PricingRuleTemplate template,
        IEnumerable<PricingRuleTemplateParameter> parameters)
    {
        return new PricingRuleTemplateResponseDto
        {
            TemplateId = template.TemplateId,
            CreatedByAdminId = template.CreatedByAdminId,
            Name = template.Name,
            Code = template.Code,
            Description = template.Description,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            Parameters = parameters
                .OrderBy(p => p.DisplayName)
                .Select(parameter => new PricingRuleTemplateParameterResponseDto
                {
                    ParameterId = parameter.ParameterId,
                    TemplateId = parameter.TemplateId,
                    ParameterKey = parameter.ParameterKey,
                    DisplayName = parameter.DisplayName,
                    DefaultValue = parameter.DefaultValue,
                    MinValue = parameter.MinValue,
                    MaxValue = parameter.MaxValue,
                    IsAdjustable = parameter.IsAdjustable
                })
                .ToList()
        };
    }

    private static Task<ApartmentPricingPolicyApplicationResponseDto> BuildApplicationResponseAsync(
        ApartmentPricingPolicyApplication application,
        PricingRuleTemplate template,
        decimal effectiveMultiplier,
        decimal basePrice)
    {
        var overrides = DeserializeOverrides(application.OverridesJson);
        return Task.FromResult(new ApartmentPricingPolicyApplicationResponseDto
        {
            ApplicationId = application.ApplicationId,
            ApartmentId = application.ApartmentId,
            TemplateId = template.TemplateId,
            TemplateName = template.Name,
            StartDate = application.StartDate,
            EndDate = application.EndDate,
            IsEnabled = application.IsEnabled,
            EffectiveMultiplier = effectiveMultiplier,
            EffectivePricePerNight = Math.Round(basePrice * effectiveMultiplier, 2, MidpointRounding.AwayFromZero),
            Overrides = overrides
        });
    }
}