using AvaloniaApplication1.DTO;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IServerService
{
    Task<PaginatedServersListDTO?> GetServersAsync(int page = 1, CancellationToken ct = default);
    Task<ServerDTO?> CreateServerAsync(string name, CancellationToken ct = default);
    Task<bool> EditServerAsync(Guid serverId, string name, CancellationToken ct = default);
    Task<bool> DeleteServerAsync(Guid serverId, CancellationToken ct = default);

    Task<List<ServerChannelDTO>> GetChannelsAsync(Guid serverId, CancellationToken ct = default);
    Task<ServerChannelDTO?> CreateChannelAsync(Guid serverId, string name, CancellationToken ct = default);
    Task<bool> EditChannelAsync(Guid serverId, Guid channelId, string name, CancellationToken ct = default);
    Task<bool> DeleteChannelAsync(Guid serverId, Guid channelId, CancellationToken ct = default);

    // Channel messages (these live under the Message controller, note "server-channels").
    Task<MessageDTO?> SendChannelMessageAsync(Guid serverId, Guid channelId, string text, CancellationToken ct = default);
    Task<PaginatedMessagesDTO?> GetChannelMessagesAsync(Guid serverId, Guid channelId, Guid? cursor = null, int pageSize = 20, CancellationToken ct = default);

    Task<List<RoleDTO>> GetRolesAsync(Guid serverId, CancellationToken ct = default);
    Task<RoleDTO?> GetRoleAsync(Guid serverId, Guid roleId, CancellationToken ct = default);
    Task<RoleDTO?> CreateRoleAsync(Guid serverId, string name, List<Permissions> actions, CancellationToken ct = default);
    Task<bool> EditRoleAsync(Guid serverId, Guid roleId, string name, List<Permissions> actions, CancellationToken ct = default);
    Task<bool> DeleteRoleAsync(Guid serverId, Guid roleId, CancellationToken ct = default);

    Task<ServerParticipantPaginatedDTO?> GetParticipantsAsync(Guid serverId, int page = 1, CancellationToken ct = default);
    Task<bool> ModifyParticipantAsync(Guid serverId, Guid participantId, Guid? roleId, string? customName, CancellationToken ct = default);
    Task<bool> RemoveParticipantAsync(Guid serverId, Guid participantId, CancellationToken ct = default);

    Task<PaginatedUserListDTO?> SearchUsersAsync(string username, int page = 1, CancellationToken ct = default);
}

public class ServerService : IServerService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ServerService(HttpClient http) => _http = http;

    public async Task<PaginatedServersListDTO?> GetServersAsync(int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/servers?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedServersListDTO>(Json, ct);
    }

    public async Task<ServerDTO?> CreateServerAsync(string name, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/v1/servers", new { Name = name }, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ServerDTO>(Json, ct);
    }

    public async Task<bool> EditServerAsync(Guid serverId, string name, CancellationToken ct = default)
    {
        var resp = await _http.PatchAsJsonAsync($"api/v1/servers/{serverId}", new { Name = name }, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteServerAsync(Guid serverId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/servers/{serverId}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<ServerChannelDTO>> GetChannelsAsync(Guid serverId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/servers/{serverId}/channels", ct);
        if (!resp.IsSuccessStatusCode) return new();
        return await resp.Content.ReadFromJsonAsync<List<ServerChannelDTO>>(Json, ct) ?? new();
    }

    public async Task<ServerChannelDTO?> CreateChannelAsync(Guid serverId, string name, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/v1/servers/{serverId}/channels", new { Name = name }, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ServerChannelDTO>(Json, ct);
    }

    public async Task<bool> EditChannelAsync(Guid serverId, Guid channelId, string name, CancellationToken ct = default)
    {
        var resp = await _http.PatchAsJsonAsync($"api/v1/servers/{serverId}/channels/{channelId}", new { Name = name }, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteChannelAsync(Guid serverId, Guid channelId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/servers/{serverId}/channels/{channelId}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<MessageDTO?> SendChannelMessageAsync(Guid serverId, Guid channelId, string text, CancellationToken ct = default)
    {
        // Confirmed route: POST api/v1/Message/servers/{serverId}/server-channels/{channelId}/send-message
        var resp = await _http.PostAsJsonAsync(
            $"api/v1/Message/servers/{serverId}/server-channels/{channelId}/send-message",
            new { Message = text },
            ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<MessageDTO>(Json, ct);
    }

    public async Task<PaginatedMessagesDTO?> GetChannelMessagesAsync(Guid serverId, Guid channelId, Guid? cursor = null, int pageSize = 20, CancellationToken ct = default)
    {
        var url = $"api/v1/Message/servers/{serverId}/server-channels/{channelId}/get-messages?pageSize={pageSize}";
        if (cursor is { } c) url += $"&cursor={c}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedMessagesDTO>(Json, ct);
    }

    public async Task<List<RoleDTO>> GetRolesAsync(Guid serverId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/servers/{serverId}/roles", ct);
        if (!resp.IsSuccessStatusCode) return new();
        return await resp.Content.ReadFromJsonAsync<List<RoleDTO>>(Json, ct) ?? new();
    }

    public async Task<RoleDTO?> GetRoleAsync(Guid serverId, Guid roleId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/servers/{serverId}/roles/{roleId}", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<RoleDTO>(Json, ct);
    }

    public async Task<RoleDTO?> CreateRoleAsync(Guid serverId, string name, List<Permissions> actions, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/v1/servers/{serverId}/roles", new { Name = name, Actions = actions }, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<RoleDTO>(Json, ct);
    }

    public async Task<bool> EditRoleAsync(Guid serverId, Guid roleId, string name, List<Permissions> actions, CancellationToken ct = default)
    {
        var resp = await _http.PatchAsJsonAsync($"api/v1/servers/{serverId}/roles/{roleId}", new { Name = name, Actions = actions }, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteRoleAsync(Guid serverId, Guid roleId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/servers/{serverId}/roles/{roleId}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<ServerParticipantPaginatedDTO?> GetParticipantsAsync(Guid serverId, int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/servers/{serverId}/participants?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ServerParticipantPaginatedDTO>(Json, ct);
    }

    public async Task<bool> ModifyParticipantAsync(Guid serverId, Guid participantId, Guid? roleId, string? customName, CancellationToken ct = default)
    {
        var resp = await _http.PatchAsJsonAsync(
            $"api/v1/servers/{serverId}/participants/{participantId}",
            new { RoleId = roleId, CustomName = customName }, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveParticipantAsync(Guid serverId, Guid participantId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/servers/{serverId}/participants/{participantId}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<PaginatedUserListDTO?> SearchUsersAsync(string username, int page = 1, CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString(username);
        var resp = await _http.GetAsync($"api/v1/User/search?username={q}&currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedUserListDTO>(Json, ct);
    }
}