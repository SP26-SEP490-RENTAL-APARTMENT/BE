using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Tests;

public class AppDbContextTests
{
    [Fact]
    public async Task Can_add_and_read_entity_with_inmemory_provider()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        await using var ctx = new AppDbContext(options);

        var amenityId = Guid.NewGuid();
        ctx.Amenities.Add(
            new Amenity
            {
                AmenityId = amenityId,
                NameEn = "Wifi",
                NameVi = "Wi-Fi",
            }
        );

        await ctx.SaveChangesAsync();

        var read = await ctx.Amenities.SingleAsync(a => a.AmenityId == amenityId);
        Assert.Equal("Wifi", read.NameEn);
    }
}
