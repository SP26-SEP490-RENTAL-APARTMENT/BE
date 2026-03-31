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
            services.AddScoped<IAuthService, AuthService>();    
            services.AddScoped<IApartmentPriceCalendarService, ApartmentPriceCalendarService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IHolidaysEventService, HolidaysEventService>();
            services.AddScoped<IInspectionPhotoService, InspectionPhotoService>();
            services.AddScoped<ILandlordService, LandlordService>();
            services.AddScoped<ILandlordSubscriptionService, LandlordSubscriptionService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<INearbyAttractionService, NearbyAttractionService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IPackageService, PackageService>();
            services.AddScoped<IPackageItemService, PackageItemService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<ILandlordWalletService, LandlordWalletService>();
            services.AddScoped<IPropertyInspectionService, PropertyInspectionService>();
            services.AddScoped<IReviewService, ReviewService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<ISmartPricingHistoryService, SmartPricingHistoryService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ISupportTicketService, SupportTicketService>();
            services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
            services.AddScoped<IMomoTransactionService, MomoTransactionService>();
            services.AddHttpClient<IMomoService, MomoService>();
            services.AddScoped<IStripeService, StripeService>();
            services.AddScoped<IResidenceReportPdfGenerator, ResidenceReportPdfGenerator>();
            services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
            services.AddScoped<IReportExecutionService, ReportExecutionService>();
            services.AddScoped<IWishlistService, WishlistService>();
            return services;
        }
    }
}