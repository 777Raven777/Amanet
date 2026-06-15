using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IChatSocket
{
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task SendAsync(OutgoingChatMessage msg, CancellationToken ct = default);
}

// Uses the built-in ClientWebSocket — no NuGet package required.
public sealed class ChatSocket : IChatSocket
{
    private readonly ISession _session;
    private readonly Uri _httpBaseAddress;          // same base URL as your HttpClient
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;

    // TODO: confirm the socket path with your backend.
    private const string SocketPath = "ws/chat";

    public ChatSocket(ISession session, Uri httpBaseAddress)
    {
        _session = session;
        _httpBaseAddress = httpBaseAddress;
    }

    public bool IsConnected => _socket is { State: WebSocketState.Open };

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected) return;

        _socket = new ClientWebSocket();

        // ClientWebSocket can set headers (unlike browser sockets). If your server
        // wants the token in the query string instead, append ?access_token=... below.
        if (_session.Token is { } token)
            _socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");

        await _socket.ConnectAsync(BuildSocketUri(), ct);

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_socket is { State: WebSocketState.Open })
        {
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
            catch { /* closing a dead socket is fine */ }
        }
        _socket?.Dispose();
        _socket = null;
    }

    public async Task SendAsync(OutgoingChatMessage msg, CancellationToken ct = default)
    {
        if (!IsConnected) return;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(msg, Json);

        // ClientWebSocket forbids concurrent sends — serialize them.
        await _sendLock.WaitAsync(ct);
        try
        {
            await _socket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (IsConnected && !ct.IsCancellationRequested)
            {
                using var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket!.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                var incoming = JsonSerializer.Deserialize<IncomingChatMessage>(json, Json);
                if (incoming is not null) Publish(incoming);
            }
        }
        catch (OperationCanceledException) { /* normal on disconnect */ }
        catch (WebSocketException) { /* connection dropped — see reconnect note */ }
        // TODO (optional): on unexpected drop, attempt ConnectAsync again after a delay.
    }

    private static void Publish(IncomingChatMessage msg) =>
        Dispatcher.UIThread.Post(() =>
            WeakReferenceMessenger.Default.Send(new ChatMessageReceived(msg)));

    private Uri BuildSocketUri()
    {
        var b = new UriBuilder(new Uri(_httpBaseAddress, SocketPath))
        {
            Scheme = _httpBaseAddress.Scheme == "https" ? "wss" : "ws"
        };
        return b.Uri;
    }
}