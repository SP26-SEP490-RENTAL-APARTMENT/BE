using Microsoft.Extensions.DependencyInjection;
using DAL.Repository.Implements;
using DAL.Repository.Interfaces;

namespace BLL.DependencyInjection
{
    public static class RepositoryRegistration
    {
        public static IServiceCollection AddRepositoryRegistration(this IServiceCollection services)
        {
            services.AddScoped<IAdminActionRepository, AdminActionRepository>();
            services.AddScoped<IAmenityRepository, AmenityRepository>();
            services.AddScoped<IApartmentRepository, ApartmentRepository>();
            services.AddScoped<IApartmentMediumRepository, ApartmentMediumRepository>();
            services.AddScoped<IApartmentPriceCalendarRepository, ApartmentPriceCalendarRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IHolidaysEventRepository, HolidaysEventRepository>();
            services.AddScoped<IInspectionPhotoRepository, InspectionPhotoRepository>();
            services.AddScoped<ILandlordRepository, LandlordRepository>();
            services.AddScoped<ILandlordSubscriptionRepository, LandlordSubscriptionRepository>();
            services.AddScoped<INearbyAttractionRepository, NearbyAttractionRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IPackageRepository, PackageRepository>();
            services.AddScoped<IPackageItemRepository, PackageItemRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<IPropertyInspectionRepository, PropertyInspectionRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<ISmartPricingHistoryRepository, SmartPricingHistoryRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
            services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
            services.AddScoped<IMomoTransactionRepository, MomoTransactionRepository>();
            services.AddScoped<ITenantWishlistRepository, TenantWishlistRepository>();
            services.AddScoped<IWishlistCollectionRepository, WishlistCollectionRepository>();
            return services;
        }
    }
}