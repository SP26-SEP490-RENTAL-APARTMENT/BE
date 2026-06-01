using System;
using System.Threading.Tasks;

public class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key) => Task.FromResult(default(T?));

    public Task SetAsync<T>(string key, T value, TimeSpan expiry) => Task.CompletedTask;
}
