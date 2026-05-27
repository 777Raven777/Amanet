namespace backend.Realtime;
public interface IRealtimeNotifier
{
    Task NotifyGroupAsync(string groupName, string eventName, object payload, CancellationToken ct = default);

    Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default);
}
