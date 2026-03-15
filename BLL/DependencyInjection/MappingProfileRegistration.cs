using BLL.Mappings;
using Microsoft.Extensions.DependencyInjection;

namespace BLL.DependencyInjection
{
    public static class MappingProfileRegistration
    {
        public static IServiceCollection AddMappingProfileRegistration(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => cfg.AddProfile<ApartmentProfile>());
            return services;
        }
    }
}