using System;
using System.Collections.Generic;

namespace Common.DTOs;

public class PricingRuleTemplateParameterDto
{
    public string ParameterKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? DisplayNameVi { get; set; }

    public decimal DefaultValue { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public bool IsAdjustable { get; set; } = true;
}

public class CreatePricingRuleTemplateDto
{
    public string Name { get; set; } = null!;

    public string? NameVi { get; set; }

    public string? Code { get; set; }

    public string? Description { get; set; }

    public string? DescriptionVi { get; set; }

    public bool IsActive { get; set; } = true;

    public List<PricingRuleTemplateParameterDto> Parameters { get; set; } = [];
}

public class PricingRuleTemplateResponseDto
{
    public Guid TemplateId { get; set; }

    public Guid CreatedByAdminId { get; set; }

    public string Name { get; set; } = null!;

    public string? NameVi { get; set; }

    public string? Code { get; set; }

    public string? Description { get; set; }

    public string? DescriptionVi { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<PricingRuleTemplateParameterResponseDto> Parameters { get; set; } = [];
}

public class PricingRuleTemplateParameterResponseDto
{
    public Guid ParameterId { get; set; }

    public Guid TemplateId { get; set; }

    public string ParameterKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? DisplayNameVi { get; set; }

    public decimal DefaultValue { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public bool IsAdjustable { get; set; }
}

public class CreateApartmentPricingPolicyApplicationDto
{
    public Guid TemplateId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsEnabled { get; set; } = true;

    public Dictionary<string, decimal> Overrides { get; set; } = new();
}

public class ApartmentPricingPolicyApplicationResponseDto
{
    public Guid ApplicationId { get; set; }

    public Guid ApartmentId { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateName { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsEnabled { get; set; }

    public decimal EffectiveMultiplier { get; set; }

    public decimal EffectivePricePerNight { get; set; }

    public Dictionary<string, decimal> Overrides { get; set; } = new();
}

public class UpdateApartmentPricingPolicyApplicationOverridesDto
{
    public Dictionary<string, decimal> Overrides { get; set; } = new();
}

public class TemplatePreviewDto
{
    public Guid ApplicationId { get; set; }
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = null!;
    public string? NameVi { get; set; }
    public string? Description { get; set; }
    public string? DescriptionVi { get; set; }
    public bool IsActive { get; set; }
    public List<PricingRuleTemplateParameterResponseDto> Parameters { get; set; } = new();
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal PreviewMultiplier { get; set; }
    public decimal PreviewPricePerNight { get; set; }
}

public class AvailableTemplatesForApartmentDto
{
    public Guid ApartmentId { get; set; }
    public decimal ApartmentBasePrice { get; set; }
    public List<TemplatePreviewDto> Templates { get; set; } = new();
}