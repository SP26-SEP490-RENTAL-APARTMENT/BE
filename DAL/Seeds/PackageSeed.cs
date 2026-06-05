using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class PackageSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var packages = new List<(Guid ApartmentId, string Name, string? NameVi, string Description, string? DescriptionVi, decimal Price)>
        {
            (SeedConstants.SeedApartmentId, "Basic Cleaning", "Dọn dẹp cơ bản", "Daily basic cleaning service", "Dịch vụ dọn dẹp cơ bản hằng ngày", 100000m),
            (SeedConstants.SeedApartment2Id, "Airport Pickup", "Đưa đón sân bay", "One-way airport pickup", "Dịch vụ đưa đón sân bay một chiều", 200000m),
            (SeedConstants.SeedApartment3Id, "Premium Experience", "Trải nghiệm cao cấp", "Welcome basket and late checkout", "Giỏ quà chào mừng và trả phòng muộn", 300000m)
        };

        foreach (var (apartmentId, name, nameVi, description, descriptionVi, price) in packages)
        {
            var existing = await context.Packages.FirstOrDefaultAsync(
                p => p.ApartmentId == apartmentId && p.Name == name,
                cancellationToken);

            if (existing is not null)
            {
                existing.NameVi = nameVi;
                existing.DescriptionVi = descriptionVi;
                context.Packages.Update(existing);
                continue;
            }

            var package = new Package
            {
                PackageId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                Name = name,
                NameVi = nameVi,
                Description = description,
                DescriptionVi = descriptionVi,
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
