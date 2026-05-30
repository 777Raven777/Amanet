using backend.Data;
using backend.Realtime;
using backend.Realtime.ConnectionTracking;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace backend.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IConnectionTracker _tracker;
    private readonly AppDbContext _db;
    private readonly PresenceService _presence;
    private readonly MessageService _messages;

    public ChatHub(IConnectionTracker tracker, AppDbContext db, PresenceService presence, MessageService messages)
    {
        _tracker = tracker;
        _db = db;
        _presence = presence;
        _messages = messages;
    }

    //connection lifecycle

    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetUserId();

        await _tracker.AddConnectionAsync(userId, Context.ConnectionId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.User(userId));

        await _presence.OnUserConnectedAsync(userId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.GetUserId();

        await _tracker.RemoveConnectionAsync(Context.ConnectionId);
        await _presence.OnUserDisconnectedAsync(userId);

        await base.OnDisconnectedAsync(exception);
    }

    //private conversations

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = Context.GetUserId();

        var isParticipant = await _db.Conversations
            .AnyAsync(c => c.Id == conversationId
                        && (c.UserLowId == userId || c.UserHighId == userId));

        if (!isParticipant)
        {
            throw new HubException("You are not a participant of this conversation.");

        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Conversation(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Conversation(conversationId));
    }

    public async Task SendPrivateMessage(Guid conversationId, string text)
    {
        var userId = Context.GetUserId();
        await _messages.SendMessageInConversationAsync(userId, conversationId, text);
    }

    public async Task SendMessageToUser(Guid recipientId, string text)
    {
        var userId = Context.GetUserId();
        await _messages.SendMessageToUserAsync(userId, recipientId, text);
    }

    public async Task TypingInConversation(Guid conversationId)
    {
        var userId = Context.GetUserId();
        await Clients
            .OthersInGroup(RealtimeGroups.Conversation(conversationId))
            .SendAsync(RealtimeEvents.UserTyping, new
            {
                UserId = userId,
                ConversationId = conversationId,
            });
    }

    //server channels

    public async Task JoinChannel(Guid channelId)
    {
        var userId = Context.GetUserId();

        var isMember = await (
            from sp in _db.ServerParticipants
            join ch in _db.ServerChannels on sp.ServerId equals ch.Server.Id
            where ch.Id == channelId && sp.Participant.Id == userId
            select sp.Id
        ).AnyAsync();

        if (!isMember)
        {
            throw new HubException("You are not a member of this channel's server.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Channel(channelId));
    }

    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Channel(channelId));
    }

    public async Task SendChannelMessage(Guid channelId, string text)
    {
        var userId = Context.GetUserId();
        await _messages.SendChannelMessageAsync(userId, channelId, text);
    }

    public async Task TypingInChannel(Guid channelId)
    {
        var userId = Context.GetUserId();
        await Clients
            .OthersInGroup(RealtimeGroups.Channel(channelId))
            .SendAsync(RealtimeEvents.UserTyping, new
            {
                UserId = userId,
                ChannelId = channelId,
            });
    }
}