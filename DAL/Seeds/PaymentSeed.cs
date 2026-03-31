using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class PaymentSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.BookingId != Guid.Empty, cancellationToken);
        if (booking is null)
            return;

        var payments = new List<Payment>
        {
            new()
            {
                PaymentId = Guid.NewGuid(),
                RelatedEntityId = booking.BookingId,
                Amount = booking.DepositAmount,
                PaymentType = "deposit",
                PaymentPurpose = "booking_deposit",
                RelatedEntityType = "Booking",
                Method = "bank_transfer",
                Status = "success",
                TransactionId = "TXN" + Guid.NewGuid().ToString().Substring(0, 8),
                PaidAt = DateTime.UtcNow
            },
            new()
            {
                PaymentId = Guid.NewGuid(),
                RelatedEntityId = booking.BookingId,
                Amount = booking.TotalPrice - booking.DepositAmount,
                PaymentType = "balance",
                PaymentPurpose = "booking_balance",
                RelatedEntityType = "Booking",
                Method = "credit_card",
                Status = "pending",
                TransactionId = null,
                PaidAt = null
            }
        };

        foreach (var payment in payments)
        {
            if (!await context.Payments.AnyAsync(p => p.PaymentId == payment.PaymentId, cancellationToken))
            {
                context.Payments.Add(payment);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
