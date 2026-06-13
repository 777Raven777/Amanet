using backend.Data;
using backend.Models.DTO;
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

        var request = new SendMessageRequest
        {
            Message = text,
            ConversationId = conversationId,
        };

        var (success, responseText, message) = await _messages.SendPrivateMessage(userId, request);

        if (!success)
        {
            throw new HubException(responseText);
        }

        await Clients
            .Group(RealtimeGroups.Conversation(conversationId))
            .SendAsync(RealtimeEvents.ReceivePrivateMessage, message);
    }

    public async Task SendMessageToUser(Guid recipientId, string text)
    {
        var userId = Context.GetUserId();

        var request = new SendMessageRequest
        {
            Message = text,
            ReceiverId = recipientId,
        };

        var (success, responseText, message) = await _messages.SendPrivateMessage(userId, request);

        if (!success)
        {
            throw new HubException(responseText);
        }

        // Notify the sender's own connections
        await Clients
            .Group(RealtimeGroups.User(userId))
            .SendAsync(RealtimeEvents.ReceivePrivateMessage, message);

        // Notify the recipient so they see the message even if they
        // haven't joined the conversation group yet
        await Clients
            .Group(RealtimeGroups.User(recipientId))
            .SendAsync(RealtimeEvents.NewMessageNotification, message);
    }

    public async Task EditPrivateMessage(Guid conversationId, Guid messageId, string newText)
    {
        var userId = Context.GetUserId();

        var (success, responseText) = await _messages.EditPrivateMessage(userId, messageId, newText);

        if (!success)
        {
            throw new HubException(responseText);
        }

        await Clients
            .Group(RealtimeGroups.Conversation(conversationId))
            .SendAsync(RealtimeEvents.MessageEdited, new
            {
                MessageId = messageId,
                ConversationId = conversationId,
                NewText = newText,
                EditedBy = userId,
            });
    }

    public async Task DeletePrivateMessage(Guid conversationId, Guid messageId)
    {
        var userId = Context.GetUserId();

        var (success, responseText) = await _messages.DeletePrivateMessage(userId, messageId);

        if (!success)
        {
            throw new HubException(responseText);
        }

        await Clients
            .Group(RealtimeGroups.Conversation(conversationId))
            .SendAsync(RealtimeEvents.MessageDeleted, new
            {
                MessageId = messageId,
                ConversationId = conversationId,
                DeletedBy = userId,
            });
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

    public async Task SendChannelMessage(Guid channelId, Guid serverId, string text)
    {
        var userId = Context.GetUserId();

        var request = new CreateOrPatchMessageDTO
        {
            Message = text,
        };

        var (success, responseText, message) = await _messages.SendChannelMessage(userId, serverId ,channelId, request);

        if (!success)
        {
            throw new HubException(responseText);
        }

        await Clients
            .Group(RealtimeGroups.Channel(channelId))
            .SendAsync(RealtimeEvents.ReceiveChannelMessage, message);
    }

    public async Task EditChannelMessage(Guid channelId, Guid messageId, Guid serverId, string newText)
    {
        var userId = Context.GetUserId();

        var (success, responseText) = await _messages.EditChannelMessage(userId, messageId, serverId, newText);

        if (!success)
        {
            throw new HubException(responseText);
        }

        await Clients
            .Group(RealtimeGroups.Channel(channelId))
            .SendAsync(RealtimeEvents.MessageEdited, new
            {
                MessageId = messageId,
                ChannelId = channelId,
                NewText = newText,
                EditedBy = userId,
            });
    }

    public async Task DeleteChannelMessage(Guid channelId, Guid messageId, Guid serverId)
    {
        var userId = Context.GetUserId();

        var (success, responseText) = await _messages.DeleteChannelMessage(userId, messageId, serverId);

        if (!success)
        {
            throw new HubException(responseText);
        }

        await Clients
            .Group(RealtimeGroups.Channel(channelId))
            .SendAsync(RealtimeEvents.MessageDeleted, new
            {
                MessageId = messageId,
                ChannelId = channelId,
                DeletedBy = userId,
            });
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