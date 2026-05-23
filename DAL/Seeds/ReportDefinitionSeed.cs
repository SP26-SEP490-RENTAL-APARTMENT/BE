using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DAL.Seeds;

public static class ReportDefinitionSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var now = Common.Utils.VietnamTime.Now;
        var adminId = SeedConstants.SeedAdminUserId;

        var catalog = new List<ReportDefinitionCatalogItem>
        {
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                "Booking Summary",
                "Booking count, revenue, occupancy, and ADR overview",
                "standard",
                "booking",
                "[{\"field\":\"date\",\"alias\":\"date\"},{\"field\":\"apartment_name\",\"alias\":\"apartment_name\"}]",
                "[{\"field\":\"booking_count\",\"aggregation\":\"count\",\"alias\":\"booking_count\"},{\"field\":\"total_revenue\",\"aggregation\":\"sum\",\"alias\":\"total_revenue\"},{\"field\":\"occupancy_percent\",\"aggregation\":\"avg\",\"alias\":\"occupancy_percent\"},{\"field\":\"adr\",\"aggregation\":\"avg\",\"alias\":\"adr\"}]",
                "{\"preset\":\"last_30_days\"}"),
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                "Revenue Performance",
                "Revenue, average booking value, and sold price trend",
                "standard",
                "revenue",
                "[{\"field\":\"date\",\"alias\":\"date\"},{\"field\":\"status\",\"alias\":\"status\"}]",
                "[{\"field\":\"total_revenue\",\"aggregation\":\"sum\",\"alias\":\"total_revenue\"},{\"field\":\"avg_booking_value\",\"aggregation\":\"avg\",\"alias\":\"avg_booking_value\"},{\"field\":\"avg_sold_price\",\"aggregation\":\"avg\",\"alias\":\"avg_sold_price\"},{\"field\":\"avg_price_delta\",\"aggregation\":\"avg\",\"alias\":\"avg_price_delta\"}]",
                "{\"preset\":\"current_month\"}"),
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                "Guest Reviews Analysis",
                "Review count and rating averages for listing quality monitoring",
                "standard",
                "review",
                "[{\"field\":\"apartment_name\",\"alias\":\"apartment_name\"}]",
                "[{\"field\":\"review_count\",\"aggregation\":\"count\",\"alias\":\"review_count\"},{\"field\":\"review_avg_rating\",\"aggregation\":\"avg\",\"alias\":\"review_avg_rating\"}]",
                "{\"preset\":\"last_90_days\"}"),
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"),
                "Subscription Performance",
                "Subscription revenue, active counts, and churn monitoring",
                "standard",
                "subscription",
                "[{\"field\":\"date\",\"alias\":\"date\"}]",
                "[{\"field\":\"subscription_active_count\",\"aggregation\":\"count\",\"alias\":\"subscription_active_count\"},{\"field\":\"subscription_revenue\",\"aggregation\":\"sum\",\"alias\":\"subscription_revenue\"},{\"field\":\"subscription_churn_count\",\"aggregation\":\"count\",\"alias\":\"subscription_churn_count\"},{\"field\":\"subscription_churn_rate\",\"aggregation\":\"avg\",\"alias\":\"subscription_churn_rate\"}]",
                "{\"preset\":\"last_30_days\"}"),
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
                "Compliance Temporary Residence",
                "Temporary residence compliance export for host/admin review",
                "standard",
                "compliance",
                "[{\"field\":\"date\",\"alias\":\"date\"},{\"field\":\"tenant_name\",\"alias\":\"tenant_name\"},{\"field\":\"apartment_name\",\"alias\":\"apartment_name\"}]",
                "[{\"field\":\"booking_count\",\"aggregation\":\"count\",\"alias\":\"booking_count\"}]",
                "{\"preset\":\"last_30_days\"}"),
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"),
                "Support Tickets Report",
                "Support ticket volume and service resolution overview",
                "standard",
                "support",
                "[{\"field\":\"date\",\"alias\":\"date\"}]",
                "[{\"field\":\"booking_count\",\"aggregation\":\"count\",\"alias\":\"booking_count\"}]",
                "{\"preset\":\"last_30_days\"}")
        };

        foreach (var item in catalog)
        {
            await UpsertReportAsync(context, item, adminId, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertReportAsync(
        AppDbContext context,
        ReportDefinitionCatalogItem item,
        Guid adminId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var report = await context.ReportDefinitions.SingleOrDefaultAsync(r => r.ReportId == item.ReportId, cancellationToken);
        if (report is null)
        {
            report = new ReportDefinition
            {
                ReportId = item.ReportId,
                Name = item.Name,
                Description = item.Description,
                Type = item.Type,
                Category = item.Category,
                IsActive = true,
                CreatedBy = adminId,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.ReportDefinitions.Add(report);
        }
        else
        {
            report.Name = item.Name;
            report.Description = item.Description;
            report.Type = item.Type;
            report.Category = item.Category;
            report.IsActive = true;
            report.CreatedBy ??= adminId;
            report.UpdatedAt = now;
            context.ReportDefinitions.Update(report);
        }

        var config = await context.Set<ReportQueryConfig>().SingleOrDefaultAsync(r => r.ReportId == item.ReportId, cancellationToken);
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
        var dimensionsJson = item.DimensionsJson;
        var metricsJson = item.MetricsJson;
        var timeRangeJson = item.TimeRangeJson;

        if (config is null)
        {
            context.Set<ReportQueryConfig>().Add(new ReportQueryConfig
            {
                ReportId = item.ReportId,
                DimensionsJson = dimensionsJson,
                MetricsJson = metricsJson,
                FiltersJson = null,
                TimeRangeJson = timeRangeJson
            });
        }
        else
        {
            config.DimensionsJson = dimensionsJson;
            config.MetricsJson = metricsJson;
            config.FiltersJson = null;
            config.TimeRangeJson = timeRangeJson;
            context.Set<ReportQueryConfig>().Update(config);
        }
    }

    private sealed record ReportDefinitionCatalogItem(
        Guid ReportId,
        string Name,
        string? Description,
        string Type,
        string Category,
        string DimensionsJson,
        string MetricsJson,
        string TimeRangeJson);
}
