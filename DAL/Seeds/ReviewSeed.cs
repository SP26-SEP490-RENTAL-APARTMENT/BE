using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class ReviewSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.Status == "confirmed", cancellationToken);
        if (booking is null)
            return;

        var tenant = await context.Users.SingleOrDefaultAsync(u => u.UserId == booking.TenantId, cancellationToken);
        var landlord = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.landlord@example.com", cancellationToken);
        var apartment = await context.Apartments.SingleOrDefaultAsync(a => a.ApartmentId == booking.ApartmentId, cancellationToken);

        if (tenant is null || landlord is null || apartment is null)
            return;

        var reviews = new List<Review>
        {
            new()
            {
                ReviewId = Guid.NewGuid(),
                BookingId = booking.BookingId,
                ReviewerId = tenant.UserId,
                ReviewedId = landlord.UserId,
                ApartmentId = apartment.ApartmentId,
                Rating = 5,
                CommentEn = "Great apartment! Very clean and comfortable. The landlord was responsive and helpful.",
                CommentVi = "Căn hộ tuyệt vời! Rất sạch sẽ và thoải mái. Chủ nhà rất phản hồi nhanh chóng.",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                ReviewId = Guid.NewGuid(),
                BookingId = booking.BookingId,
                ReviewerId = landlord.UserId,
                ReviewedId = tenant.UserId,
                ApartmentId = apartment.ApartmentId,
                Rating = 4,
                CommentEn = "Good guest! Respectful and left the apartment in good condition.",
                CommentVi = "Khách tốt! Tôn trọng và để lại căn hộ trong tình trạng tốt.",
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var review in reviews)
        {
            if (!await context.Reviews.AnyAsync(r => r.ReviewId == review.ReviewId, cancellationToken))
            {
                context.Reviews.Add(review);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
