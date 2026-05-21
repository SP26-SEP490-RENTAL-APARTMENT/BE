using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoMoApi;
using Common.Settings;
using PayOS;

namespace BLL.DependencyInjection
{
    public static class ConfigurationRegistration
    {
        public static IServiceCollection AddConfigurationRegistration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
            services.Configure<FrontendSettings>(configuration.GetSection("Frontend"));
            services.Configure<CloudinarySettings>(configuration.GetSection("CloudinarySettings"));
            services.Configure<MomoOptions>(configuration.GetSection("Momo"));
            services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
            services.Configure<Common.Settings.PayOsOptions>(configuration.GetSection("PayOs"));
            services.Configure<FptIdRecognitionOptions>(configuration.GetSection(FptIdRecognitionOptions.SectionName));

            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();

                var clientId = config["PayOS:ClientId"] ?? Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID") ?? string.Empty;
                var apiKey = config["PayOS:ApiKey"] ?? Environment.GetEnvironmentVariable("PAYOS_API_KEY") ?? string.Empty;
                var checksumKey = config["PayOS:ChecksumKey"] ?? Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY") ?? string.Empty;

                return new PayOSClient(new PayOSOptions
                {
                    ClientId = clientId,
                    ApiKey = apiKey,
                    ChecksumKey = checksumKey,
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information
                });
            });

            return services;
        }
    }
}