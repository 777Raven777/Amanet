using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public static class HubExtensions
{
    public static Guid GetUserId(this HubCallerContext context)
    {
        var raw = context.UserIdentifier;
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var id))
            throw new HubException("Unauthenticated.");
        return id;
    }
}