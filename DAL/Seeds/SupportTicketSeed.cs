using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class SupportTicketSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var tenant = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.tenant@example.com", cancellationToken);
        var landlord = await context.Users.SingleOrDefaultAsync(u => u.Email == "seed.landlord@example.com", cancellationToken);
        
        if (tenant is null || landlord is null)
            return;

        var tickets = new List<SupportTicket>
        {
            new()
            {
                TicketId = Guid.NewGuid(),
                UserId = tenant.UserId,
                Subject = "Facility not working in apartment",
                Description = "The air conditioner in my booked apartment is not working properly.",
                Category = "property_quality",
                Priority = "high",
                Status = "open",
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                TicketId = Guid.NewGuid(),
                UserId = landlord.UserId,
                Subject = "Payment verification needed",
                Description = "I need to verify my payment for the recent booking.",
                Category = "payment_problem",
                Priority = "medium",
                Status = "in_progress",
                CreatedAt = Common.Utils.VietnamTime.Now.AddDays(-1),
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                TicketId = Guid.NewGuid(),
                UserId = tenant.UserId,
                Subject = "Booking cancellation request",
                Description = "I need to cancel my upcoming booking due to schedule change.",
                Category = "cancellation",
                Priority = "medium",
                Status = "resolved",
                CreatedAt = Common.Utils.VietnamTime.Now.AddDays(-3),
                UpdatedAt = Common.Utils.VietnamTime.Now
            }
        };

        foreach (var ticket in tickets)
        {
            if (!await context.SupportTickets.AnyAsync(t => t.TicketId == ticket.TicketId, cancellationToken))
            {
                context.SupportTickets.Add(ticket);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
