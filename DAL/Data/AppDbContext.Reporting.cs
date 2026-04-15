using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Map reporting entities explicitly to match the existing snake_case MySQL schema.
        modelBuilder.Entity<ReportDefinition>(entity =>
        {
            entity.ToTable("report_definitions");
            entity.HasKey(e => e.ReportId);

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // One-to-one: ReportDefinition (principal) -> ReportQueryConfig (dependent)
            entity.HasOne(e => e.QueryConfig)
                .WithOne(q => q.Report)
                .HasForeignKey<ReportQueryConfig>(q => q.ReportId);

            entity.HasMany(e => e.ScheduledReports)
                .WithOne(s => s.Report)
                .HasForeignKey(s => s.ReportId);

            entity.HasMany(e => e.GeneratedReports)
                .WithOne(g => g.Report)
                .HasForeignKey(g => g.ReportId);
        });

        modelBuilder.Entity<ReportQueryConfig>(entity =>
        {
            entity.ToTable("report_query_configs");
            entity.HasKey(e => e.ReportId);

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.DimensionsJson).HasColumnName("dimensions_json");
            entity.Property(e => e.FiltersJson).HasColumnName("filters_json");
            entity.Property(e => e.MetricsJson).HasColumnName("metrics_json");
            entity.Property(e => e.TimeRangeJson).HasColumnName("time_range_json");
        });

        modelBuilder.Entity<ScheduledReport>(entity =>
        {
            entity.ToTable("scheduled_reports");
            entity.HasKey(e => e.ScheduledReportId);

            entity.Property(e => e.ScheduledReportId).HasColumnName("scheduled_report_id");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Frequency).HasColumnName("frequency");
            entity.Property(e => e.CronExpression).HasColumnName("cron_expression");
            entity.Property(e => e.NextRunAt).HasColumnName("next_run_at");
            entity.Property(e => e.LastRunAt).HasColumnName("last_run_at");
            entity.Property(e => e.DeliveryChannel).HasColumnName("delivery_channel");
            entity.Property(e => e.Recipients).HasColumnName("recipients");
        });

        modelBuilder.Entity<GeneratedReport>(entity =>
        {
            entity.ToTable("generated_reports");
            entity.HasKey(e => e.GeneratedReportId);

            entity.Property(e => e.GeneratedReportId).HasColumnName("generated_report_id");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.RequestedAt).HasColumnName("requested_at");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ResultSummaryJson).HasColumnName("result_summary_json");
            entity.Property(e => e.ResultJson).HasColumnName("result_json");
            entity.Property(e => e.RetentionUntil).HasColumnName("retention_until");
        });
    }
}
