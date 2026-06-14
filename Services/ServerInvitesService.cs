using AvaloniaApplication1.DTO;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IServerInvitesService
{
    Task<ServerInviteDTO?> SendAsync(Guid serverId, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid serverId, Guid inviteId, CancellationToken ct = default);
    Task<bool> AcceptAsync(Guid inviteId, CancellationToken ct = default);
    Task<bool> RejectAsync(Guid inviteId, CancellationToken ct = default);
    Task<ReceivedServerInvitePaginatedDTO?> ListReceivedServerInvitesAsync(int page = 1, CancellationToken ct = default);
    Task<ServerInvitePaginatedDTO?> ListSentServerInvitesAsync(Guid serverId, int page = 1, CancellationToken ct = default);
}

public class ServerInvitesService : IServerInvitesService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ServerInvitesService(HttpClient http) => _http = http;

    public async Task<ServerInviteDTO?> SendAsync(Guid serverId, Guid userId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/v1/Servers/{serverId}/invites", new { InvitedUserId = userId }, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ServerInviteDTO>(Json, ct);
    }

    public async Task<bool> AcceptAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/v1/User/server-invites/{id}/accept-invite", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RejectAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/v1/User/server-invites/{id}/reject-invite", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid serverId, Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/Servers/{serverId}/invites/{id}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<ReceivedServerInvitePaginatedDTO?> ListReceivedServerInvitesAsync(int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/User/server-invites?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ReceivedServerInvitePaginatedDTO>(Json, ct);
    }

    public async Task<ServerInvitePaginatedDTO?> ListSentServerInvitesAsync(Guid serverId, int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/Servers/{serverId}/invites?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ServerInvitePaginatedDTO>(Json, ct);
    }
}

