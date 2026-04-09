using Common.Enums;
using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class RoomSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Rooms.AnyAsync(cancellationToken))
        {
            return;
        }

        var rooms = new List<Room>
        {
            new()
            {
                RoomId = SeedConstants.SeedRoom1Id,
                ApartmentId = SeedConstants.SeedApartmentId,
                Title = "Cozy Master Room",
                Description = "A comfortable master room with an ensuite bathroom and great city view.",
                RoomType = RoomTypes.private_single.ToString(),
                BedType = BedTypes.queen.ToString(),
                SizeSqm = 25m,
                IsPrivateBathroom = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                RoomId = SeedConstants.SeedRoom2Id,
                ApartmentId = SeedConstants.SeedApartment2Id,
                Title = "Standard Single Room",
                Description = "A standard single room, well-furnished.",
                RoomType = RoomTypes.private_single.ToString(),
                BedType = BedTypes.single.ToString(),
                SizeSqm = 15m,
                IsPrivateBathroom = false,
                CreatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                RoomId = SeedConstants.SeedRoom3Id,
                ApartmentId = SeedConstants.SeedApartment3Id,
                Title = "Spacious Double Room",
                Description = "A double room suitable for couples.",
                RoomType = RoomTypes.private_double.ToString(),
                BedType = BedTypes.@double.ToString(),
                SizeSqm = 20m,
                IsPrivateBathroom = true,
                CreatedAt = Common.Utils.VietnamTime.Now
            }
        };

        foreach (var room in rooms)
        {
            if (!await context.Rooms.AnyAsync(r => r.RoomId == room.RoomId, cancellationToken))
            {
                context.Rooms.Add(room);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
