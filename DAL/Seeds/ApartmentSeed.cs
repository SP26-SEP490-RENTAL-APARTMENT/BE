using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace DAL.Seeds;

public static class ApartmentSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlordUser = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.landlord@example.com", cancellationToken);
        if (landlordUser is null)
        {
            return;
        }

        var apartments = new List<Apartment>
        {
            new()
            {
                ApartmentId = SeedConstants.SeedApartmentId,
                LandlordId = landlordUser.UserId,
                Title = "Seed Apartment 1",
                Description = "Seed apartment description 1",
                Address = "1 Seed Street",
                District = "District 1",
                City = "Hồ Chí Minh",
                Latitude = 10.776889m,
                Longitude = 106.700806m,
                Location = new Point(106.700806, 10.776889) { SRID = 4326 },
                BasePricePerNight = 1000000m,
                IsPetAllowed = false,
                MaxOccupants = 1,
                MaxPets = 0,
                Status = "draft",
                BookingStatus = "locked",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                ApartmentId = SeedConstants.SeedApartment2Id,
                LandlordId = landlordUser.UserId,
                Title = "Seed Apartment 2",
                Description = "Seed apartment description 2",
                Address = "2 Seed Street",
                District = "District 3",
                City = "Hồ Chí Minh",
                Latitude = 10.784500m,
                Longitude = 106.689300m,
                Location = new Point(106.689300, 10.784500) { SRID = 4326 },
                BasePricePerNight = 1500000m,
                IsPetAllowed = true,
                MaxOccupants = 2,
                MaxPets = 2,
                Status = "posted",
                BookingStatus = "available",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                ApartmentId = SeedConstants.SeedApartment3Id,
                LandlordId = landlordUser.UserId,
                Title = "Seed Apartment 3",
                Description = "Seed apartment description 3",
                Address = "3 Seed Street",
                District = "Bình Thạnh",
                City = "Hồ Chí Minh",
                Latitude = 10.802300m,
                Longitude = 106.712900m,
                Location = new Point(106.712900, 10.802300) { SRID = 4326 },
                BasePricePerNight = 900000m,
                IsPetAllowed = false,
                MaxOccupants = 3,
                MaxPets = 0,
                Status = "posted",
                BookingStatus = "available",
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var apartment in apartments)
        {
            if (!await context.Apartments.AnyAsync(a => a.ApartmentId == apartment.ApartmentId, cancellationToken))
            {
                context.Apartments.Add(apartment);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
