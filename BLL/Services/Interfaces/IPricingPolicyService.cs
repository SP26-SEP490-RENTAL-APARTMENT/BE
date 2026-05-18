using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IPricingPolicyService
{
    Task<IEnumerable<PricingRuleTemplateResponseDto>> GetTemplatesAsync();

    Task<PricingRuleTemplateResponseDto> CreateTemplateAsync(CreatePricingRuleTemplateDto dto, Guid adminId);

    Task<PricingRuleTemplateResponseDto> UpdateTemplateAsync(Guid templateId, CreatePricingRuleTemplateDto dto, Guid adminId);

    Task<PricingRuleTemplateResponseDto> SetTemplateStatusAsync(Guid templateId, bool isActive, Guid adminId);

    Task<ApartmentPricingPolicyApplicationResponseDto> ApplyTemplateAsync(
        Guid apartmentId,
        CreateApartmentPricingPolicyApplicationDto dto,
        Guid landlordId);

    Task<ApartmentPricingPolicyApplicationResponseDto> SetApplicationStatusAsync(
        Guid apartmentId,
        Guid applicationId,
        bool isEnabled,
        Guid landlordId);

    Task<ApartmentPricingPolicyApplicationResponseDto> UpdateApplicationOverridesAsync(
        Guid apartmentId,
        Guid applicationId,
        UpdateApartmentPricingPolicyApplicationOverridesDto dto,
        Guid landlordId);

    Task<AvailableTemplatesForApartmentDto> GetAvailableTemplatesForApartmentAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate);
}