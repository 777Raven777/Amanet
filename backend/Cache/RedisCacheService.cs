using StackExchange.Redis;
using System.Text.Json;

namespace backend.Cache;

public class RedisCacheService : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly IDatabase db;
    private readonly string keyPrefix;

    public RedisCacheService(IConnectionMultiplexer redis, IConfiguration config)
    {
        db = redis.GetDatabase();
        keyPrefix = config["Redis:InstanceName"] ?? string.Empty;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var jsonData = JsonSerializer.Serialize(value);
        await db.StringSetAsync(keyPrefix + key, jsonData, ttl ?? DefaultTtl);
    }


    public async Task<T?> GetAsync<T>(string key)
    {
        var jsonData = await db.StringGetAsync(keyPrefix + key);

        if (jsonData.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>((string)jsonData!);
    }

    public async Task RemoveAsync(string key)
    {
        await db.KeyDeleteAsync(keyPrefix + key);
    }
}