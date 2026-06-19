using Avalonia.Threading;
using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

public interface IChatHub
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task JoinChannelAsync(Guid channelId);
    Task SendChannelMessageAsync(Guid channelId, Guid serverId, string text);
    Task JoinConversationAsync(Guid conversationId);
    Task SendPrivateMessageAsync(Guid conversationId, string text);

    Task LeaveChannelAsync(Guid channelId);
}

public sealed class ChatHubClient : IChatHub
{
    private readonly ISession _session;
    private readonly Uri _baseAddress;
    private HubConnection? _hub;

    public ChatHubClient(ISession session, Uri baseAddress)
    {
        _session = session;
        _baseAddress = baseAddress;
    }

    public async Task ConnectAsync()
    {
        if (_hub is { State: HubConnectionState.Connected }) return;

        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_baseAddress, "chathub"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_session.Token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<MessageDTO>("ReceivePrivateMessage", m =>
            Dispatcher.UIThread.Post(() =>
                WeakReferenceMessenger.Default.Send(new PrivateMessageReceived(m))));

        _hub.On<MessageDTO>("ReceiveChannelMessage", m =>
            Dispatcher.UIThread.Post(() =>
                WeakReferenceMessenger.Default.Send(new ChannelMessageReceived(m))));

        await _hub.StartAsync();
    }

    public Task JoinChannelAsync(Guid channelId) =>
        _hub!.InvokeAsync("JoinChannel", channelId);

    public Task SendChannelMessageAsync(Guid channelId, Guid serverId, string text) =>
        _hub!.InvokeAsync("SendChannelMessage", channelId, serverId, text);

    public Task JoinConversationAsync(Guid conversationId) =>
        _hub!.InvokeAsync("JoinConversation", conversationId);

    public Task SendPrivateMessageAsync(Guid conversationId, string text) =>
        _hub!.InvokeAsync("SendPrivateMessage", conversationId, text);

    public Task LeaveChannelAsync(Guid channelId) =>
        _hub!.InvokeAsync("LeaveChannel", channelId);

    public async Task DisconnectAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
        _hub = null;
    }
}