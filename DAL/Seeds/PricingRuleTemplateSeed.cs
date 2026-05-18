using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace DAL.Seeds;

public static class PricingRuleTemplateSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        const string code = "DEFAULT_PRICING_POLICY";
        const string name = "Default pricing policy";

        var template = await context.PricingRuleTemplates.FirstOrDefaultAsync(
            t => t.Code == code || t.Name == name,
            cancellationToken);

        if (template is null)
        {
            template = new PricingRuleTemplate
            {
                TemplateId = Guid.NewGuid(),
                CreatedByAdminId = SeedConstants.SeedAdminUserId,
                Name = name,
                Code = code,
                Description = "Default multipliers for standard/weekend/holiday pricing. Landlords can enable and adjust bounded parameters.",
                IsActive = true,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            };

            context.PricingRuleTemplates.Add(template);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                context.Entry(template).State = EntityState.Detached;
                template = await context.PricingRuleTemplates.FirstAsync(
                    t => t.Code == code || t.Name == name,
                    cancellationToken);
            }
        }

        async Task UpsertParameterAsync(string key, string displayName, decimal defaultValue, decimal? minValue, decimal? maxValue)
        {
            var existing = await context.PricingRuleTemplateParameters.FirstOrDefaultAsync(
                p => p.TemplateId == template.TemplateId && p.ParameterKey == key,
                cancellationToken);

            if (existing is not null)
            {
                existing.DisplayName = displayName;
                existing.DefaultValue = defaultValue;
                existing.MinValue = minValue;
                existing.MaxValue = maxValue;
                existing.IsAdjustable = true;
                context.PricingRuleTemplateParameters.Update(existing);
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var parameter = new PricingRuleTemplateParameter
            {
                ParameterId = Guid.NewGuid(),
                TemplateId = template.TemplateId,
                ParameterKey = key,
                DisplayName = displayName,
                DefaultValue = defaultValue,
                MinValue = minValue,
                MaxValue = maxValue,
                IsAdjustable = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            };

            context.PricingRuleTemplateParameters.Add(parameter);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                context.Entry(parameter).State = EntityState.Detached;
            }
        }

        await UpsertParameterAsync("weekend_multiplier", "Weekend multiplier", 1.2m, 1.0m, 3.0m);
        await UpsertParameterAsync("holiday_multiplier", "Holiday multiplier", 1.5m, 1.0m, 4.0m);
    }
}
