using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class ConfigurationRegistration
    {
        public static IServiceCollection AddConfigurationRegistration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Common.Settings.JwtSettings>(configuration.GetSection("Jwt"));
            return services;
        }
    }
}