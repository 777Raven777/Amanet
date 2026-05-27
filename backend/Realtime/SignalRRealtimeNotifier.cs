using backend.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace backend.Realtime;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<ChatHub> hub;

    public SignalRRealtimeNotifier(IHubContext<ChatHub> hub)
    {
        this.hub = hub;
    }

    public Task NotifyGroupAsync(string groupName, string eventName, object payload, CancellationToken ct = default)
    {
        return hub.Clients.Group(groupName).SendAsync(eventName, payload, ct);
    }

    public Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default)
    {
        return hub.Clients.Group(RealtimeGroups.User(userId)).SendAsync(eventName, payload, ct);
    }
}