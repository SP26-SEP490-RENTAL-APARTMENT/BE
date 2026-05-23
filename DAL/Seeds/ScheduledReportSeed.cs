using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class ScheduledReportSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var now = Common.Utils.VietnamTime.Now;

        var schedules = new List<ScheduledReportCatalogItem>
        {
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                "weekly",
                "0 8 * * 1",
                now.AddDays(1),
                null,
                "email",
                "host-reports@example.com"),
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                "monthly",
                "0 8 1 * *",
                now.AddDays(2),
                null,
                "email",
                "host-reports@example.com"),
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"),
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                "daily",
                "0 7 * * *",
                now.AddHours(12),
                null,
                "email",
                "host-reports@example.com"),
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4"),
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
                "daily",
                "0 6 * * *",
                now.AddHours(6),
                null,
                "email",
                "compliance@example.com")
        };

        foreach (var item in schedules)
        {
            await UpsertScheduleAsync(context, item, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertScheduleAsync(
        AppDbContext context,
        ScheduledReportCatalogItem item,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var schedule = await context.ScheduledReports.SingleOrDefaultAsync(s => s.ScheduledReportId == item.ScheduledReportId, cancellationToken);
        if (schedule is null)
        {
            schedule = new ScheduledReport
            {
                ScheduledReportId = item.ScheduledReportId,
                ReportId = item.ReportId,
                Frequency = item.Frequency,
                CronExpression = item.CronExpression,
                NextRunAt = item.NextRunAt,
                LastRunAt = item.LastRunAt,
                DeliveryChannel = item.DeliveryChannel,
                Recipients = item.Recipients
            };

            context.ScheduledReports.Add(schedule);
            return;
        }

        schedule.ReportId = item.ReportId;
        schedule.Frequency = item.Frequency;
        schedule.CronExpression = item.CronExpression;
        schedule.NextRunAt = item.NextRunAt;
        schedule.LastRunAt = item.LastRunAt;
        schedule.DeliveryChannel = item.DeliveryChannel;
        schedule.Recipients = item.Recipients;
        context.ScheduledReports.Update(schedule);
    }

    private sealed record ScheduledReportCatalogItem(
        Guid ScheduledReportId,
        Guid ReportId,
        string Frequency,
        string CronExpression,
        DateTime? NextRunAt,
        DateTime? LastRunAt,
        string DeliveryChannel,
        string Recipients);
}