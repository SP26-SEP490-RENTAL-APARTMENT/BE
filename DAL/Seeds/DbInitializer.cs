using DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        // Integration tests use InMemory provider, which does not support relational migrations.
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        // Core entities
        await UserSeed.SeedAsync(context, cancellationToken);
        await AmenitySeed.SeedAsync(context, cancellationToken);
        await LandlordSeed.SeedAsync(context, cancellationToken);
        await ApartmentSeed.SeedAsync(context, cancellationToken);
        await RoomSeed.SeedAsync(context, cancellationToken);
        await SubscriptionPlanSeed.SeedAsync(context, cancellationToken);
        await HolidaysEventSeed.SeedAsync(context, cancellationToken);
        await NearbyAttractionSeed.SeedAsync(context, cancellationToken);
        await PackageSeed.SeedAsync(context, cancellationToken);

        // Dependent entities
        await LandlordWalletSeed.SeedAsync(context, cancellationToken);
        await LandlordSubscriptionSeed.SeedAsync(context, cancellationToken);
        // TODO: Fix unique constraint for reviews
        // await ReviewSeed.SeedAsync(context, cancellationToken);
        await SupportTicketSeed.SeedAsync(context, cancellationToken);
        await PropertyInspectionSeed.SeedAsync(context, cancellationToken);
        await SmartPricingHistorySeed.SeedAsync(context, cancellationToken);
        // TODO: Fix duplicate key constraint - data already seeded
        // await ApartmentPriceCalendarSeed.SeedAsync(context, cancellationToken);
        // TODO: Debug ReportDefinitionSeed error
        // await ReportDefinitionSeed.SeedAsync(context, cancellationToken);
    }
}
