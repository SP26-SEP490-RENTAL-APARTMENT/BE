using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace DAL.Seeds;

public static class NearbyAttractionSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var attractions = new List<NearbyAttraction>
        {
            new()
            {
                NameEn = "Central Park",
                NameVi = "Công viên trung tâm",
                Type = "park",
                City = "Hồ Chí Minh",
                Address = "District 1",
                Location = new Point(106.7008, 10.7769) { SRID = 4326 }
            },
            new()
            {
                NameEn = "Historic Museum",
                NameVi = "Bảo tàng lịch sử",
                Type = "museum",
                City = "Hồ Chí Minh",
                Address = "District 1",
                Location = new Point(106.7030, 10.7798) { SRID = 4326 }
            },
            new()
            {
                NameEn = "Night Market",
                NameVi = "Chợ đêm",
                Type = "shopping",
                City = "Hồ Chí Minh",
                Address = "District 1",
                Location = new Point(106.6920, 10.7720) { SRID = 4326 }
            }
        };

        foreach (var attraction in attractions)
        {
            var exists = await context.NearbyAttractions.AnyAsync(
                a => a.NameEn == attraction.NameEn && a.City == attraction.City,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            attraction.AttractionId = Guid.NewGuid();
            context.NearbyAttractions.Add(attraction);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
