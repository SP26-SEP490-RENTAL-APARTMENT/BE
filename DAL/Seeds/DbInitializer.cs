using DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.MigrateAsync(cancellationToken);

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
        await BookingSeed.SeedAsync(context, cancellationToken);
        await PaymentSeed.SeedAsync(context, cancellationToken);
        await ReviewSeed.SeedAsync(context, cancellationToken);
        await SupportTicketSeed.SeedAsync(context, cancellationToken);
        await PropertyInspectionSeed.SeedAsync(context, cancellationToken);
        await SmartPricingHistorySeed.SeedAsync(context, cancellationToken);
        await ApartmentPriceCalendarSeed.SeedAsync(context, cancellationToken);
        await ReportDefinitionSeed.SeedAsync(context, cancellationToken);
    }
}
