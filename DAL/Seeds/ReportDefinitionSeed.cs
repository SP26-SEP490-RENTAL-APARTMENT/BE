using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class ReportDefinitionSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var adminUser = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.admin@example.com", cancellationToken);
        if (adminUser is null)
            return;

        var reportDefinitions = new List<ReportDefinition>
        {
            new()
            {
                ReportId = Guid.NewGuid(),
                Name = "Monthly Revenue Report",
                Description = "Monthly revenue summary for all apartments",
                Type = "scheduled",
                Category = "revenue",
                IsActive = true,
                CreatedBy = adminUser.UserId,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                ReportId = Guid.NewGuid(),
                Name = "Booking Summary",
                Description = "Summary of all bookings and occupancy rates",
                Type = "standard",
                Category = "booking",
                IsActive = true,
                CreatedBy = adminUser.UserId,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                ReportId = Guid.NewGuid(),
                Name = "Guest Reviews Analysis",
                Description = "Analysis of guest reviews and ratings",
                Type = "standard",
                Category = "review",
                IsActive = true,
                CreatedBy = adminUser.UserId,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                ReportId = Guid.NewGuid(),
                Name = "Support Tickets Report",
                Description = "Summary of support tickets and resolution rates",
                Type = "standard",
                Category = "support",
                IsActive = true,
                CreatedBy = adminUser.UserId,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            }
        };

        foreach (var report in reportDefinitions)
        {
            if (!await context.ReportDefinitions.AnyAsync(r => r.ReportId == report.ReportId, cancellationToken))
            {
                context.ReportDefinitions.Add(report);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
