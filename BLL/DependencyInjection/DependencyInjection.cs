using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBLLDependencies(this IServiceCollection services)
        {
            services.AddConfigurationRegistration();
            services.AddMappingProfileRegistration();
            services.AddRepositoryRegistration();
            services.AddServiceRegistration();

            return services;
        }
    }
}