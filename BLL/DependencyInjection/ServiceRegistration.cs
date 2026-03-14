using Microsoft.Extensions.DependencyInjection;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using DAL.Repository.Implements;
using DAL.Repository.Interfaces;

namespace BLL.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddServiceRegistration(this IServiceCollection services)
        {
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            services.AddScoped<IAmenityService, AmenityService>();
            services.AddScoped<IAdminActionService, AdminActionService>();
            services.AddScoped<IApartmentService, ApartmentService>();
            services.AddScoped<IApartmentMediumService, ApartmentMediumService>();
            services.AddScoped<IApartmentPriceCalendarService, ApartmentPriceCalendarService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IHolidaysEventService, HolidaysEventService>();
            services.AddScoped<IInspectionPhotoService, InspectionPhotoService>();
            services.AddScoped<ILandlordService, LandlordService>();
            services.AddScoped<ILandlordSubscriptionService, LandlordSubscriptionService>();
            services.AddScoped<INearbyAttractionService, NearbyAttractionService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IPackageService, PackageService>();
            services.AddScoped<IPackageItemService, PackageItemService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IPropertyInspectionService, PropertyInspectionService>();
            services.AddScoped<IReviewService, ReviewService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<ISmartPricingHistoryService, SmartPricingHistoryService>();

            return services;
        }
    }
}