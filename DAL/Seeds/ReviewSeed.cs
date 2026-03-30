using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class ReviewSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var bookings = await context.Bookings.Where(b => b.Status == "confirmed").ToListAsync(cancellationToken);
        if (!bookings.Any())
            return;

        var booking = bookings[0];      
        var tenant = await context.Users.SingleOrDefaultAsync(u => u.UserId == booking.TenantId, cancellationToken);
        var landlord = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.landlord@example.com", cancellationToken);
        var apartment = await context.Apartments.SingleOrDefaultAsync(a => a.ApartmentId == booking.ApartmentId, cancellationToken);

        if (tenant is null || landlord is null || apartment is null)
            return;

        var review = new Review
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
        };

        if (!await context.Reviews.AnyAsync(r => r.ReviewId == review.ReviewId, cancellationToken))
        {
            context.Reviews.Add(review);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
