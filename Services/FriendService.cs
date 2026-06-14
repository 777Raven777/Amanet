using AvaloniaApplication1.DTO;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IFriendService
{
    Task<List<FriendDTO>> GetFriendsAsync(int page = 1, CancellationToken ct = default);
    Task<PaginatedUserListDTO?> SearchUserAsync(string username, int page = 1, CancellationToken ct = default);
    Task<PaginatedRequestListDTO?> GetReceivedAsync(int page = 1, CancellationToken ct = default);
    Task<PaginatedRequestListDTO?> GetSentAsync(int page = 1, CancellationToken ct = default);
    Task<bool> AcceptAsync(Guid relationshipId, CancellationToken ct = default);
    Task<bool> RejectAsync(Guid relationshipId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid relationshipId, CancellationToken ct = default);

    Task<bool> SendAsync(string userId, CancellationToken ct = default);
}

public class FriendService : IFriendService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public FriendService(HttpClient http) => _http = http;

    public async Task<PaginatedUserListDTO?> SearchUserAsync(string username, int page = 1, CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString(username);
        var resp = await _http.GetAsync($"api/v1/User/search?username={q}&currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedUserListDTO?>(Json, ct);
    }

    public async Task<List<FriendDTO>> GetFriendsAsync(int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/Friends/friends-list?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return new();
        return await resp.Content.ReadFromJsonAsync<List<FriendDTO>>(Json, ct) ?? new();
    }

    public async Task<bool> SendAsync(string id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/v1/Friends", new { Id = id }, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> AcceptAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/v1/Friends/{id}/accept-request", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RejectAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/v1/Friends/{id}/reject-request", null, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/Friends/{id}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<PaginatedRequestListDTO?> GetReceivedAsync(int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/Friends/requests/received?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedRequestListDTO>(Json, ct);
    }
    public async Task<PaginatedRequestListDTO?> GetSentAsync(int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/Friends/requests/sent?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedRequestListDTO>(Json, ct);
    }
}