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
            services.Configure<FrontendSettings>(configuration.GetSection("Frontend"));
            services.Configure<CloudinarySettings>(configuration.GetSection("CloudinarySettings"));
            services.Configure<MomoOptions>(configuration.GetSection("Momo"));
            services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
            services.Configure<Common.Settings.PayOsOptions>(configuration.GetSection("PayOs"));
            services.Configure<FptIdRecognitionOptions>(configuration.GetSection(FptIdRecognitionOptions.SectionName));
                // Configure PayOS SDK client
                services.AddSingleton(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var options = new PayOS.PayOSOptions
                    {
                        ClientId = config["PayOS:ClientId"],
                        ApiKey = config["PayOS:ApiKey"],
                        ChecksumKey = config["PayOS:ChecksumKey"],
                        LogLevel = Microsoft.Extensions.Logging.LogLevel.Debug
                    };
                    return new PayOS.PayOSClient(options);
                });

                // Register SDK adapter for easier testing/mocking
                services.AddSingleton<BLL.Services.Interfaces.IPayOsClientAdapter>(sp =>
                {
                    var client = sp.GetRequiredService<PayOS.PayOSClient>();
                    return new BLL.Services.Implements.PayOsSdkAdapter(client);
                });
            return services;
        }
    }
}