using System.Configuration;
using System.Security.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

public static class RedisRegistration
{
    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["RedisConnectionString"]
            ?? configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            // Log: Redis not configured, skipping Redis registration
            return services;
        }

        try
        {
            // Parse connection string and explicitly enforce TLS
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            options.Ssl = true;
            options.SslProtocols = SslProtocols.Tls12;
            options.AllowAdmin = false;
            options.ConnectTimeout = 5000;

            var redisConnection = ConnectionMultiplexer.Connect(options);

            // Register the connection multiplexer for RedisCacheService to consume
            services.AddSingleton<IConnectionMultiplexer>(redisConnection);

            return services;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to Redis: {ex.Message}");
            return services;
        }
    }
}

public interface IConfigService
{

}

public class ConfigService : IConfigService
{
    private readonly IDatabase _database;

    public ConfigService(IConnectionMultiplexer connection)
    {
        // Get a handle to the database instance
        _database = connection.GetDatabase();
    }
}
