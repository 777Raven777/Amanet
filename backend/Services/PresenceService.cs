using backend.Data;
using backend.Models;
using backend.Models.DTO;
using backend.Realtime;
using backend.Realtime.ConnectionTracking;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class PresenceService
{
    private readonly IConnectionTracker _tracker;
    private readonly IRealtimeNotifier _notifier;
    private readonly AppDbContext _db;

    public PresenceService(IConnectionTracker tracker, IRealtimeNotifier notifier, AppDbContext db)
    {
        _tracker = tracker;
        _notifier = notifier;
        _db = db;
    }

    public async Task OnUserConnectedAsync(Guid userId, CancellationToken ct = default)
    {
        var connections = await _tracker.GetConnectionsAsync(userId);
        if (connections.Count != 1)
        {
            return;
        }

        await NotifyFriendsAsync(userId, "online", ct);
    }

    public async Task OnUserDisconnectedAsync(Guid userId, CancellationToken ct = default)
    {
        var stillOnline = await _tracker.IsOnlineAsync(userId);
        if (stillOnline)
        {
            return;
        }

        await NotifyFriendsAsync(userId, "offline", ct);
    }

    private async Task NotifyFriendsAsync(Guid userId, string status, CancellationToken ct)
    {
        var friendIds = await _db.Relationships
            .Where(r => r.Status == RelationshipType.Accepted
                     && (r.SenderId == userId || r.ReceiverId == userId))
            .Select(r => r.SenderId == userId ? r.ReceiverId : r.SenderId)
            .ToListAsync(ct);

        if (friendIds.Count == 0)
        {
            return;
        }

        var payload = new PresenceChangedDTO
        {
            UserId = userId,
            Status = status,
        };

        foreach (var friendId in friendIds)
        {
            await _notifier.NotifyUserAsync(friendId, RealtimeEvents.PresenceChanged, payload, ct);
        }
    }
}