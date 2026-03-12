using DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class ConfigurationRegistration
    {
        public static IServiceCollection AddConfigurationRegistration(this IServiceCollection services)
        {
            // Add configuration registrations here
            return services;
        }
    }
}