using backend.Data;
using backend.Models;
using backend.Models.DTO;
using backend.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class MessageService
{
    private const int MaxMessageLength = 4000;

    private readonly AppDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public MessageService(AppDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }


    public async Task<PrivateMessageDTO> SendMessageInConversationAsync(
        Guid senderId,
        Guid conversationId,
        string text,
        CancellationToken ct = default)
    {
        ValidateText(text);

        var conv = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Id, c.UserLowId, c.UserHighId })
            .FirstOrDefaultAsync(ct);

        if (conv == null)
        {
            throw new HubException("Conversation not found.");
        }
           
        if (conv.UserLowId != senderId && conv.UserHighId != senderId)
        {

            throw new HubException("You are not a participant of this conversation.");
        }

        var otherUserId = conv.UserLowId == senderId ? conv.UserHighId : conv.UserLowId;

        return await PersistAndBroadcastAsync(senderId, conv.Id, otherUserId, text, ct);
    }

    public async Task<PrivateMessageDTO> SendMessageToUserAsync(
        Guid senderId,
        Guid recipientId,
        string text,
        CancellationToken ct = default)
    {
        ValidateText(text);

        if (senderId == recipientId)
        {
            throw new HubException("You cannot message yourself.");
        }

        var (low, high) = OrderUserPair(senderId, recipientId);

        var existing = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.UserLowId == low && c.UserHighId == high)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);

        Guid conversationId;

        if (existing != null)
        {
            conversationId = existing.Id;
        }
        else
        {
            await EnsureCanInitiateAsync(senderId, recipientId, ct);

            var created = new Conversation
            {
                UserLowId = low,
                UserHighId = high,
            };

            _db.Conversations.Add(created);

            try
            {
                await _db.SaveChangesAsync(ct);
                conversationId = created.Id;
            }
            catch (DbUpdateException)
            {
                var winner = await _db.Conversations
                    .AsNoTracking()
                    .Where(c => c.UserLowId == low && c.UserHighId == high)
                    .Select(c => new { c.Id })
                    .FirstAsync(ct);
                conversationId = winner.Id;
            }
        }

        return await PersistAndBroadcastAsync(senderId, conversationId, recipientId, text, ct);
    }


    public async Task<ChannelMessageDTO> SendChannelMessageAsync(
        Guid senderId,
        Guid channelId,
        string text,
        CancellationToken ct = default)
    {
        ValidateText(text);

        var channel = await _db.ServerChannels
            .AsNoTracking()
            .Where(c => c.Id == channelId)
            .Select(c => new { ChannelId = c.Id, ServerId = c.Server.Id })
            .FirstOrDefaultAsync(ct);

        if (channel == null)
        {
            throw new HubException("Channel not found.");
        }
            
        var isMember = await _db.ServerParticipants
            .AnyAsync(sp => sp.ServerId == channel.ServerId && sp.Participant.Id == senderId, ct);

        if (!isMember)
        {
            throw new HubException("You are not a member of this channel's server.");
        }
           

        var sender = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == senderId)
            .Select(u => new { u.Username, u.ProfilePictureUrl })
            .FirstOrDefaultAsync(ct);

        if (sender == null)
        {

            throw new HubException("Sender not found.");
        }

        var message = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ServerChannelId = channelId,
            Text = text.Trim(),
            Edited = false,
        };

        _db.ChannelMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = new ChannelMessageDTO
        {
            Id = message.Id,
            ChannelId = channelId,
            SenderId = senderId,
            SenderUsername = sender.Username,
            SenderProfilePictureUrl = sender.ProfilePictureUrl,
            Text = message.Text,
            CreatedAt = message.CreatedAt,
            Edited = false,
        };

        await _notifier.NotifyGroupAsync(RealtimeGroups.Channel(channelId), RealtimeEvents.ReceiveChannelMessage, dto, ct);

        return dto;
    }

    private async Task<PrivateMessageDTO> PersistAndBroadcastAsync(
        Guid senderId,
        Guid conversationId,
        Guid otherUserId,
        string text,
        CancellationToken ct)
    {
        var sender = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == senderId)
            .Select(u => new { u.Username, u.ProfilePictureUrl })
            .FirstOrDefaultAsync(ct);

        if (sender == null)
        {
            throw new HubException("Sender not found.");
        }
            

        var message = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ConversationId = conversationId,
            Text = text.Trim(),
            Edited = false,
        };

        _db.PrivateMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = new PrivateMessageDTO
        {
            Id = message.Id,
            ConversationId = conversationId,
            SenderId = senderId,
            SenderUsername = sender.Username,
            SenderProfilePictureUrl = sender.ProfilePictureUrl,
            Text = message.Text,
            CreatedAt = message.CreatedAt,
            Edited = false,
        };

        await _notifier.NotifyGroupAsync(RealtimeGroups.Conversation(conversationId), RealtimeEvents.ReceivePrivateMessage, dto, ct);

        await _notifier.NotifyUserAsync(otherUserId, RealtimeEvents.NewMessageNotification, dto, ct);

        return dto;
    }

    private async Task EnsureCanInitiateAsync(Guid senderId, Guid recipientId, CancellationToken ct)
    {
        var recipient = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == recipientId)
            .Select(u => new { u.Id, u.OnlyFriendsMessages })
            .FirstOrDefaultAsync(ct);

        if (recipient == null)
        {
            throw new HubException("Recipient not found.");
        }
            

        if (!recipient.OnlyFriendsMessages)
        {
            return;
        }
            
        var areFriends = await _db.Relationships.AnyAsync(r =>
            r.Status == RelationshipType.Accepted &&
            ((r.SenderId == senderId && r.ReceiverId == recipientId) ||
             (r.SenderId == recipientId && r.ReceiverId == senderId)), ct);

        if (!areFriends)
        {
            throw new HubException("This user only accepts messages from friends.");
        }    
    }

    private static (Guid Low, Guid High) OrderUserPair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new HubException("Message text cannot be empty.");
        }


        if (text.Length > MaxMessageLength)
        {
            throw new HubException($"Message text cannot exceed {MaxMessageLength} characters.");
        }
    }
}