using StackExchange.Redis;

namespace backend.Realtime.ConnectionTracking;

public class RedisConnectionTracker : IConnectionTracker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _keyPrefix;

    public RedisConnectionTracker(IConnectionMultiplexer redis, IConfiguration config)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _keyPrefix = config["Redis:InstanceName"] ?? string.Empty;
    }

    private string UserKey(Guid userId) => $"{_keyPrefix}conn:user:{userId}";
    private string IdKey(string connectionId) => $"{_keyPrefix}conn:id:{connectionId}";
    private string IdKeyPattern => $"{_keyPrefix}conn:id:*";

    public async Task AddConnectionAsync(Guid userId, string connectionId)
    {
        var tran = _db.CreateTransaction();
        _ = tran.SetAddAsync(UserKey(userId), connectionId);
        _ = tran.StringSetAsync(IdKey(connectionId), userId.ToString());

        bool committed = await tran.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException(
                $"Failed to register connection {connectionId} for user {userId}.");
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        var userIdValue = await _db.StringGetAsync(IdKey(connectionId));
        if (userIdValue.IsNullOrEmpty)
            return;

        if (!Guid.TryParse(userIdValue, out Guid userId))
        {
            await _db.KeyDeleteAsync(IdKey(connectionId));
            return;
        }

        var tran = _db.CreateTransaction();
        _ = tran.SetRemoveAsync(UserKey(userId), connectionId);
        _ = tran.KeyDeleteAsync(IdKey(connectionId));
        await tran.ExecuteAsync();
    }

    public async Task<IReadOnlyList<string>> GetConnectionsAsync(Guid userId)
    {
        var members = await _db.SetMembersAsync(UserKey(userId));
        return members
            .Where(m => !m.IsNullOrEmpty)
            .Select(m => m.ToString())
            .ToList();
    }

    public async Task<bool> IsOnlineAsync(Guid userId)
    {
        long count = await _db.SetLengthAsync(UserKey(userId));
        return count > 0;
    }

    public Task<IReadOnlyList<string>> GetAllConnectionIdsAsync()
    {
        var result = new List<string>();
        var prefixLen = $"{_keyPrefix}conn:id:".Length;

        var endpoints = _redis.GetEndPoints();
        if (endpoints.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>(result);
        }
            
        var server = _redis.GetServer(endpoints[0]);
        foreach (var key in server.Keys(pattern: IdKeyPattern, pageSize: 250))
        {
            var str = key.ToString();
            if (str.Length > prefixLen)
                result.Add(str.Substring(prefixLen));
        }

        return Task.FromResult<IReadOnlyList<string>>(result);
    }
}