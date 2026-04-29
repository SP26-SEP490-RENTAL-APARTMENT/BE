using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBLLDependencies(this IServiceCollection services, Microsoft.Extensions.Configuration.ConfigurationManager configuration)
        {
            services.AddConfigurationRegistration(configuration);
            services.AddMappingProfileRegistration();
            services.AddRepositoryRegistration();
            services.AddServiceRegistration();
            services.AddRedisServices(configuration);

            return services;
        }
    }
}