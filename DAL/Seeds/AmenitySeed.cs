using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class AmenitySeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var amenities = new (string en, string vi)[]
        {
            ("WiFi", "WiFi"),
            ("Air conditioning", "Máy lạnh"),
            ("Kitchen", "Bếp"),
            ("Washing machine", "Máy giặt"),
            ("Parking", "Chỗ đậu xe"),
            ("Elevator", "Thang máy"),
            ("Swimming pool", "Hồ bơi"),
            ("Gym", "Phòng gym"),
            ("Hot water", "Nước nóng"),
            ("TV", "Tivi")
        };

        foreach (var (en, vi) in amenities)
        {
            var exists = await context.Amenities.AnyAsync(a => a.NameEn == en, cancellationToken);
            if (exists)
                continue;

            context.Amenities.Add(new Amenity
            {
                AmenityId = Guid.NewGuid(),
                NameEn = en,
                NameVi = vi
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
