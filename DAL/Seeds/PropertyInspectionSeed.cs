using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class PropertyInspectionSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var staffUser = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.staff@example.com", cancellationToken);
        var apartment = await context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId != Guid.Empty, cancellationToken);

        if (staffUser is null || apartment is null)
            return;

        var inspections = new List<PropertyInspection>
        {
            new()
            {
                InspectionId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                InspectorId = staffUser.UserId,
                ScheduledDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now.AddDays(-5)),
                CompletedDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now.AddDays(-5)),
                Status = "passed",
                OverallCondition = "excellent",
                IssuesFound = null,
                Recommendations = "Regular maintenance scheduled for next quarter.",
                ApprovedForListing = true,
                ApprovedAt = Common.Utils.VietnamTime.Now.AddDays(-5),
                ApprovedBy = staffUser.UserId
            },
            new()
            {
                InspectionId = Guid.NewGuid(),
                ApartmentId = apartment.ApartmentId,
                InspectorId = staffUser.UserId,
                ScheduledDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now.AddDays(10)),
                CompletedDate = null,
                Status = "scheduled",
                OverallCondition = null,
                IssuesFound = null,
                Recommendations = null,
                ApprovedForListing = null,
                ApprovedAt = null,
                ApprovedBy = null
            }
        };

        foreach (var inspection in inspections)
        {
            if (!await context.PropertyInspections.AnyAsync(i => i.InspectionId == inspection.InspectionId, cancellationToken))
            {
                context.PropertyInspections.Add(inspection);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
