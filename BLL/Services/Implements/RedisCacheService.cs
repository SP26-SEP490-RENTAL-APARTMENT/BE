using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;
using System;

/// <summary>
/// Concrete implementation of ICacheService using Redis.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    // Constructor takes the ConnectionMultiplexer instance, ensuring proper connection management.
    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
    }

    /// <summary>
    /// Retrieves a serialized object from Redis, deserializing it back to type T.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            RedisValue cachedValue = await _db.StringGetAsync(key);

            if (cachedValue.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(cachedValue.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetAsync failed for key {key}: {ex.Message}");
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
    {
        if (value == null)
            return;

        try
        {
            var jsonString = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, jsonString, expiry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SetAsync failed for key {key}: {ex.Message}");
        }
    }
}
