using System;

namespace DAL.Models;

public partial class PricingRuleTemplateParameter
{
    public Guid ParameterId { get; set; }

    public Guid TemplateId { get; set; }

    public string ParameterKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public decimal DefaultValue { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public bool IsAdjustable { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual PricingRuleTemplate Template { get; set; } = null!;
}