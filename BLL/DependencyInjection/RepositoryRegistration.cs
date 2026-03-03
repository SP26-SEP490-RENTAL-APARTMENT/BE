using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class RepositoryRegistration
    {
        public static IServiceCollection AddRepositoryRegistration(this IServiceCollection services)
        {
            // Add repository registrations here
            return services;
        }
    }
}