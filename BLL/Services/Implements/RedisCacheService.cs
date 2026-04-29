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
        // Execute Redis GET command
        RedisValue cachedValue = await _db.StringGetAsync(key);

        if (cachedValue.IsNullOrEmpty)
        {
            return default;
        }

        // Deserialize the JSON string back into the expected type T
        try
        {
            var jsonString = cachedValue.ToString();
            T? result = JsonSerializer.Deserialize<T>(jsonString);
            return result;
        }
        catch (JsonException ex)
        {
            // Log the deserialization error
            Console.WriteLine($"Error deserializing cached item for key {key}: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Serializes the object to JSON and sets it in Redis with a Time-To-Live (TTL).
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
    {
        if (value == null)
        {
            return;
        }

        // Serialize the object into a JSON string
        var jsonString = JsonSerializer.Serialize(value);
        
        // Execute Redis SET command with the expiry option
        await _db.StringSetAsync(key, jsonString, expiry);
    }
}
