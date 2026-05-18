namespace backend.Cache;

/// <summary>
/// Generic caching interface for storing, retrieving, and invalidating cached data.
/// Inject this interface into your services to use Redis caching.
/// </summary>
/// <example>
/// Registration (already done in Program.cs):
/// <code>
/// builder.Services.AddSingleton&lt;ICacheService, RedisCacheService&gt;();
/// </code>
/// 
/// Usage in a service:
/// <code>
/// public class ServerService
/// {
///     private readonly ICacheService _cache;
///     
///     public ServerService(ICacheService cache)
///     {
///         _cache = cache;
///     }
///     
///     public async Task&lt;Server?&gt; GetServerAsync(int serverId)
///     {
///         var key = $"server:{serverId}";
///         var cached = await _cache.GetAsync&lt;Server&gt;(key);
///         if (cached != null) return cached;
///         
///         var server = await _db.Servers.FindAsync(serverId);
///         if (server != null)
///             await _cache.SetAsync(key, server, TimeSpan.FromMinutes(15));
///         
///         return server;
///     }
/// }
/// </code>
/// </example>
public interface ICacheService
{
    /// <summary>
    /// Stores a value in Redis cache.
    /// </summary>
    /// <typeparam name="T">The type of object to cache. Must be JSON-serializable.</typeparam>
    /// <param name="key">Cache key. Use "entity:id" format, e.g. "server:5".</param>
    /// <param name="value">The object to cache.</param>
    /// <param name="ttl">Time to live. Defaults to 5 minutes if not specified.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// Retrieves a value from Redis cache.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="key">Cache key to look up.</param>
    /// <returns>The cached object, or default(T) if not found.</returns>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Removes a value from Redis cache. Call this after any DB write
    /// to the corresponding entity to prevent stale data.
    /// </summary>
    /// <param name="key">Cache key to invalidate.</param>
    Task RemoveAsync(string key);
}