using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace backend.Services;

public class NameIdUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}