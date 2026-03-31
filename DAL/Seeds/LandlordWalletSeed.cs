using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class LandlordWalletSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlord = await context.Landlords.SingleOrDefaultAsync(l => l.LandlordId != Guid.Empty, cancellationToken);
        if (landlord is null)
            return;

        if (!await context.LandlordWallets.AnyAsync(w => w.LandlordId == landlord.LandlordId, cancellationToken))
        {
            context.LandlordWallets.Add(new LandlordWallet
            {
                LandlordId = landlord.LandlordId,
                PendingBalance = 0m,
                AvailableBalance = 0m,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
