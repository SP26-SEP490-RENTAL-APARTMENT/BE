using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class LandlordWalletSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlordIds = await context.Landlords
            .Where(l => l.LandlordId != Guid.Empty)
            .Select(l => l.LandlordId)
            .ToListAsync(cancellationToken);

        if (landlordIds.Count == 0) return;

        var existingWalletIds = await context.LandlordWallets
            .Where(w => landlordIds.Contains(w.LandlordId))
            .Select(w => w.LandlordId)
            .ToListAsync(cancellationToken);

        var missingWallets = landlordIds
            .Except(existingWalletIds)
            .Select(id => new LandlordWallet
            {
                LandlordId = id,
                PendingBalance = 0m,
                AvailableBalance = 0m,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        if (missingWallets.Count == 0) return;

        context.LandlordWallets.AddRange(missingWallets);
        await context.SaveChangesAsync(cancellationToken);
    }
}
