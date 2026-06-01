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

            services.AddSingleton<ICacheService, NullCacheService>();

            return services;

        }



        try

        {

            var options = BuildConfigurationOptions(redisConnectionString);

            var redisConnection = ConnectionMultiplexer.Connect(options);

            services.AddSingleton<IConnectionMultiplexer>(redisConnection);

            services.AddSingleton<ICacheService, RedisCacheService>();

            return services;

        }

        catch (Exception ex)

        {

            Console.WriteLine($"Error connecting to Redis: {ex.Message}. Falling back to NullCacheService.");

            services.AddSingleton<ICacheService, NullCacheService>();

            return services;

        }

    }



    private static ConfigurationOptions BuildConfigurationOptions(string connectionString)

    {

        connectionString = connectionString.Trim();



        if (connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)

            || connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))

        {

            return BuildFromRedisUri(connectionString);

        }



        var options = ConfigurationOptions.Parse(connectionString);

        ApplyDefaults(options);

        if (options.Ssl)

        {

            options.SslProtocols = SslProtocols.Tls12;

        }



        return options;

    }



    private static ConfigurationOptions BuildFromRedisUri(string connectionString)

    {

        var uri = new Uri(connectionString);

        var useSsl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);



        string? password = null;

        if (!string.IsNullOrEmpty(uri.UserInfo))

        {

            var colon = uri.UserInfo.IndexOf(':');

            password = colon >= 0

                ? Uri.UnescapeDataString(uri.UserInfo[(colon + 1)..])

                : Uri.UnescapeDataString(uri.UserInfo);

        }



        var port = uri.Port > 0 ? uri.Port : (useSsl ? 6380 : 6379);



        var options = new ConfigurationOptions

        {

            Ssl = useSsl,

            AllowAdmin = false,

        };



        if (!string.IsNullOrEmpty(password))

        {

            options.Password = password;

        }



        options.EndPoints.Add($"{uri.Host}:{port}");

        ApplyDefaults(options);



        if (useSsl)

        {

            options.SslProtocols = SslProtocols.Tls12;

        }



        return options;

    }



    private static void ApplyDefaults(ConfigurationOptions options)

    {

        options.AbortOnConnectFail = false;

        options.ConnectTimeout = 15000;

        options.SyncTimeout = 15000;

        options.AsyncTimeout = 15000;

        options.AllowAdmin = false;

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

        _database = connection.GetDatabase();

    }

}


