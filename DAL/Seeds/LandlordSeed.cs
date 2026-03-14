using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class LandlordSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlordUser = await context.Users.SingleOrDefaultAsync(
            u => u.Email == "seed.landlord@example.com",
            cancellationToken
        );

        if (landlordUser is null)
        {
            return;
        }

        if (await context.Landlords.AnyAsync(l => l.LandlordId == landlordUser.UserId, cancellationToken))
        {
            return;
        }

        context.Landlords.Add(new Landlord
        {
            LandlordId = landlordUser.UserId,
            CurrentPlanId = null,
            SubscriptionStatus = "none",
            IdentityVerificationStatus = "not_started",
            VerifiedBusiness = false,
            LastVerifiedAt = null,
            SubscriptionExpiresAt = null
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
