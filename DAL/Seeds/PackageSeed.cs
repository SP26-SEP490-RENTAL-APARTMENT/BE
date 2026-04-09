using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class PackageSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var packages = new List<(Guid ApartmentId, string Name, string Description, decimal Price)>
        {
            (SeedConstants.SeedApartmentId, "Basic Cleaning", "Daily basic cleaning service", 100000m),
            (SeedConstants.SeedApartment2Id, "Airport Pickup", "One-way airport pickup", 200000m),
            (SeedConstants.SeedApartment3Id, "Premium Experience", "Welcome basket and late checkout", 300000m)
        };

        foreach (var (apartmentId, name, description, price) in packages)
        {
            var exists = await context.Packages.AnyAsync(
                p => p.ApartmentId == apartmentId && p.Name == name,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            var package = new Package
            {
                PackageId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                Name = name,
                Description = description,
                Price = price,
                Currency = "VND",
                IsActive = true,
                MaxBookings = null,
                CreatedAt = Common.Utils.VietnamTime.Now
            };

            context.Packages.Add(package);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
