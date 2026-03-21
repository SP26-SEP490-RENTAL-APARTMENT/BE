using BLL.Mappings;
using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class MappingProfileRegistration
    {
        public static IServiceCollection AddMappingProfileRegistration(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => cfg.AddProfile<ApartmentProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<SubscriptionPlanProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<BookingProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<SupportTicketProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<UserProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<RoomProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<PackageProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<PropertyInspectionProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<ReviewProfile>());
            services.AddAutoMapper(cfg => cfg.AddProfile<NearbyAttractionProfile>());
            return services;
        }
    }
}