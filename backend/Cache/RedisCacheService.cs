using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace backend.Cache;

/// <summary>
/// Redis implementation of <see cref="ICacheService"/>.
/// Uses IDistributedCache under the hood — do not inject IDistributedCache
/// directly in your services, use ICacheService instead.
/// </summary>
/// <remarks>
/// Registered as singleton in Program.cs:
/// <code>
/// builder.Services.AddStackExchangeRedisCache(options =>
/// {
///     options.Configuration = builder.Configuration["Redis:ConnectionString"];
///     options.InstanceName = builder.Configuration["Redis:InstanceName"];
/// });
/// builder.Services.AddSingleton&lt;ICacheService, RedisCacheService&gt;();
/// </code>
/// 
/// Requires Redis to be running. Start with Docker:
/// <code>
/// docker run --name redis -p 6379:6379 -d redis
/// </code>
/// 
/// secrets.json
/// <code>
/// {
///     "Redis:ConnectionString": "[url]:[port],password=[password]",
///     "Redis:InstanceName": "[project name]:"
/// }
/// </code>
/// </remarks>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var expiration = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(5)
        };
        var jsonData = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, jsonData, expiration);
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key)
    {
        var jsonData = await _cache.GetStringAsync(key);
        return jsonData == null ? default : JsonSerializer.Deserialize<T>(jsonData);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}