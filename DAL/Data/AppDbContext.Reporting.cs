using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Configure primary keys explicitly for reporting entities
        modelBuilder.Entity<ReportDefinition>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            // One-to-one: ReportDefinition (principal) -> ReportQueryConfig (dependent)
            entity.HasOne(e => e.QueryConfig)
                .WithOne(q => q.Report)
                .HasForeignKey<ReportQueryConfig>(q => q.ReportId);
        });

        modelBuilder.Entity<ReportQueryConfig>(entity =>
        {
            entity.HasKey(e => e.ReportId);
        });
    }
}
