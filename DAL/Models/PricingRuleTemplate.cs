using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PricingRuleTemplate
{
    public Guid TemplateId { get; set; }

    public Guid CreatedByAdminId { get; set; }

    public string Name { get; set; } = null!;

    public string? Code { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<PricingRuleTemplateParameter> Parameters { get; set; } = new List<PricingRuleTemplateParameter>();
}