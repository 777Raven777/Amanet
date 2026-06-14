using AvaloniaApplication1.DTO;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IConversationService
{
    Task<PaginatedConversationsDTO?> GetConversationsAsync(int page = 1, CancellationToken ct = default);
    Task<PaginatedMessagesDTO?> GetMessagesAsync(Guid conversationId, Guid? cursor = null, int pageSize = 20, CancellationToken ct = default);
}

public class ConversationService : IConversationService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public ConversationService(HttpClient http) => _http = http;

    public async Task<PaginatedConversationsDTO?> GetConversationsAsync(int page = 1, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/v1/Message/dms?currentPage={page}&pageSize=20", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedConversationsDTO>(Json, ct);
    }

    public async Task<PaginatedMessagesDTO?> GetMessagesAsync(Guid conversationId, Guid? cursor = null, int pageSize = 20, CancellationToken ct = default)
    {
        var url = $"api/v1/Message/dms/{conversationId}/get-messages?pageSize={pageSize}";
        if (cursor is { } c) url += $"&cursor={c}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PaginatedMessagesDTO>(Json, ct);
    }
}