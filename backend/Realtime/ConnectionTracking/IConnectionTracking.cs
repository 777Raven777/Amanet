namespace backend.Realtime.ConnectionTracking;

public interface IConnectionTracker
{
    Task AddConnectionAsync(Guid userId, string connectionId);

    Task RemoveConnectionAsync(string connectionId);

    Task<IReadOnlyList<string>> GetConnectionsAsync(Guid userId);

    Task<bool> IsOnlineAsync(Guid userId);

    Task<IReadOnlyList<string>> GetAllConnectionIdsAsync();
}
