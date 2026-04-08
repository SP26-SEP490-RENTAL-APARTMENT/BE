using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class BookingSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var tenantUser = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.tenant@example.com", cancellationToken);
        if (tenantUser is null)
            return;

        var apartment2 = await context.Apartments.SingleOrDefaultAsync(a => a.ApartmentId == SeedConstants.SeedApartment2Id, cancellationToken);
        if (apartment2 is null)
            return;

        var bookings = new List<Booking>
        {
            new()
            {
                BookingId = Guid.NewGuid(),
                TenantId = tenantUser.UserId,
                ApartmentId = apartment2.ApartmentId,
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                Nights = 3,
                NoOfAdults = 2,
                NoOfInfants = 0,
                NoOfPets = 0,
                TotalPrice = 4500000m,
                PackageId = null,
                PackagePrice = null,
                DepositAmount = 2250000m,
                UpfrontPaymentAmount = 2250000m,
                DepositPaid = false,
                PaymentMode = "partial",
                BalanceDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)),
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                BookingId = Guid.NewGuid(),
                TenantId = tenantUser.UserId,
                ApartmentId = SeedConstants.SeedApartment3Id,
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(27)),
                Nights = 7,
                NoOfAdults = 1,
                NoOfInfants = 0,
                NoOfPets = 0,
                TotalPrice = 6300000m,
                PackageId = null,
                PackagePrice = null,
                DepositAmount = 3150000m,
                UpfrontPaymentAmount = 6300000m,
                DepositPaid = true,
                PaymentMode = "full",
                BalanceDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(26)),
                Status = "paid",
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var booking in bookings)
        {
            if (!await context.Bookings.AnyAsync(b => b.BookingId == booking.BookingId, cancellationToken))
            {
                context.Bookings.Add(booking);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
