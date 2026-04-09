using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoMoApi;
using Common.Settings;

namespace BLL.DependencyInjection
{
    public static class ConfigurationRegistration
    {
        public static IServiceCollection AddConfigurationRegistration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
            services.Configure<CloudinarySettings>(configuration.GetSection("CloudinarySettings"));
            services.Configure<MomoOptions>(configuration.GetSection("Momo"));
            services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
            return services;
        }
    }
}