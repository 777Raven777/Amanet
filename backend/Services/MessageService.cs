using backend.Data;
using backend.Models;
using backend.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace backend.Services;

public class MessageService
{
    private readonly AppDbContext _context;

    public MessageService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string ResponseText, MessageDTO? Message)> SendPrivateMessage(Guid callerId, SendMessageRequest request)
    {
        // NOTE: Validation (Message non-empty, ConversationId/ReceiverId XOR) lives on
        // SendMessageRequest via IValidatableObject + [ApiController] model validation.
        // Service assumes a structurally valid request.

        string cleanText = request.Message.Trim();

        Conversation conversation;
        if (request.ConversationId != null)
        {
            var (ok, msg, existing) = await LoadExistingConversation(request.ConversationId.Value, callerId);
            if (!ok) return (false, msg, null);
            conversation = existing!;
        }
        else
        {
            var (ok, msg, resolved) = await ResolveOrPrepareConversation(callerId, request.ReceiverId!.Value);
            if (!ok) return (false, msg, null);
            conversation = resolved!;
        }

        return await PersistMessage(conversation, callerId, cleanText);
    }

    private async Task<(bool Success, string Message, Conversation? Conversation)> LoadExistingConversation(
    Guid conversationId, Guid callerId)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId);

        if (conversation == null)
            return (false, "Provided conversation Id is not valid", null);

        if (conversation.User1Id != callerId && conversation.User2Id != callerId)
            return (false, "You are not a part of this conversation", null);

        return (true, string.Empty, conversation);
    }

    private async Task<(bool Success, string Message, Conversation? Conversation)> ResolveOrPrepareConversation(
    Guid callerId, Guid receiverId)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == receiverId);
        if (receiver == null)
            return (false, "This user does not exist", null);

        // NOTE: TOCTOU gap — receiver.OnlyFriendsMessages and the friendship row can change
        // between this check and the message insert below. Accepted for current scope:
        // policy on "delete friendship vs deliver in-flight message" is undecided.
        // If addressed later, SELECT FOR UPDATE on the receiver row + Relationship row
        // would close it, but the policy question must be answered first. SO ＼（〇_ｏ）／
        if (receiver.OnlyFriendsMessages && !await AreFriends(callerId, receiver.Id))
            return (false, "This user only allows friends to message them.", null);

        var (user1Id, user2Id) = OrderPair(callerId, receiverId);

        var existing = await _context.Conversations.FirstOrDefaultAsync(x =>
            x.User1Id == user1Id && x.User2Id == user2Id);

        if (existing != null)
            return (true, string.Empty, existing);

        // No existing conversation — stage a new one. Not saved yet; PersistMessage
        // will SaveChanges once for both conversation + message (single transaction,
        // no orphan conversations on partial failure).
        var staged = new Conversation { User1Id = user1Id, User2Id = user2Id };
        _context.Conversations.Add(staged);
        return (true, string.Empty, staged);
    }

    private async Task<bool> AreFriends(Guid a, Guid b)
    {
        return await _context.Relationships.AnyAsync(x =>
            x.Status == RelationshipType.Accepted &&
            ((x.SenderId == a && x.ReceiverId == b) ||
             (x.SenderId == b && x.ReceiverId == a)));
    }

    private static (Guid User1Id, Guid User2Id) OrderPair(Guid a, Guid b)
    {
        // Convention enforced at DB level via CHECK (User1Id < User2Id) + UNIQUE(User1Id, User2Id).
        // Application-side sort is defense-in-depth: avoids a round-trip on every insert
        // just to discover we violated the constraint.
        return a.CompareTo(b) < 0 ? (a, b) : (b, a);
    }

    private async Task<(bool Success, string ResponseText, MessageDTO? Message)> PersistMessage(
    Conversation conversation, Guid callerId, string text)
    {
        var message = new PrivateMessage
        {
            Conversation = conversation,
            SenderId = callerId,
            Text = text,
        };
        _context.PrivateMessages.Add(message);

        var senderInfo = await _context.Users
        .AsNoTracking()
        .Where(u => u.Id == callerId)
        .Select(u => new { u.Username, u.ProfilePictureUrl })
        .FirstOrDefaultAsync();

        if (senderInfo == null)
        {
            return (false, "Sender account not found.", null);
        }

        try
        {
            await _context.SaveChangesAsync();
            return (true, "Message was successfully sent", new MessageDTO
                {
                    Id = message.Id,
                    SentAt = message.CreatedAt,
                    Sender = new UserDTO
                    {
                        Id = callerId,
                        Username = senderInfo.Username,
                        ProfilePictureUrl = senderInfo.ProfilePictureUrl,
                    },
                    Message = text,
                }
            );
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Race: another request created the same conversation between our find and our save.
            // Postgres SQLSTATE 23505 = unique_violation on (User1Id, User2Id).
            // Detach our losing entity, fetch the winner, re-stage the message against it, save once more.
            _context.Entry(conversation).State = EntityState.Detached;
            _context.Entry(message).State = EntityState.Detached;

            var winner = await _context.Conversations.FirstAsync(c =>
                c.User1Id == conversation.User1Id && c.User2Id == conversation.User2Id);

            _context.PrivateMessages.Add(new PrivateMessage
            {
                ConversationId = winner.Id,
                SenderId = callerId,
                Text = text,
            });
            await _context.SaveChangesAsync();
            return (true, "Message was successfully sent", new MessageDTO
            {
                Id = message.Id,
                SentAt = message.CreatedAt,
                Sender = new UserDTO
                {
                    Id = callerId,
                    Username = senderInfo.Username,
                    ProfilePictureUrl = senderInfo.ProfilePictureUrl,
                },
                Message = text,
            }
            );
        }
    }

    public async Task<(bool, string, PaginatedMessagesDTO?)> GetPrivateMessages(Guid callerId, Guid conversationId, int pageSize, Guid? currentCursor = null)
    {
        var exists = await _context.Conversations.AnyAsync(x => x.Id == conversationId
                                        && (x.User1Id == callerId || x.User2Id == callerId));
        if (!exists)
        {
            return (false, "You are not a part of this conversation", null);
        }

        var query = _context.PrivateMessages.AsQueryable();
        query = query.Where(m => m.ConversationId == conversationId);

        var response = await PaginateMessageList(pageSize, currentCursor, query);

        return (true, "Messages retrieved", response);
    }

    public async Task<(bool, string, PaginatedMessagesDTO?)> GetServerChannelMessages(Guid callerId, Guid serverChannelId, int pageSize, Guid? currentCursor = null)
    {
        var channel = await _context.ServerChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == serverChannelId);
        string vagueMessage = "You are not a participant of this server, or it does not exist";

        if (channel == null)
        {
            return (false, vagueMessage, null); // We return this on purpose, so that no one can guess id
        }

        bool isParticipent = await _context.ServerParticipants.AnyAsync(p => p.ParticipantId == callerId
                            && p.ServerId == channel.ServerId);

        if (!isParticipent)
        {
            return (false, vagueMessage, null);
        }

        var query = _context.ChannelMessages.AsQueryable();
        query = query.Where(m => m.ServerChannelId == serverChannelId);

        var response = await PaginateMessageList(pageSize, currentCursor, query);
        return (true, "Messages retrieved", response);
    }

    private async Task<PaginatedMessagesDTO> PaginateMessageList(int pageSize, Guid? currentCursor, IQueryable<BaseMessage> query)
    {
        if (currentCursor != null)
        {
            query = query.Where(m => m.Id <= currentCursor);
        }

        var result = await query.OrderByDescending(m => m.Id)
                    .Take(pageSize + 1)
                    .Select(m => new MessageDTO
                    {
                        Id = m.Id,
                        Message = m.Text,
                        SentAt = m.CreatedAt,
                        Sender = new UserDTO { Id = m.Sender.Id, Username = m.Sender.Username }
                    }
                    ).ToListAsync();

        bool hasMore = result.Count > pageSize;
        Guid? nextCursor = hasMore ? result[^1].Id : null;

        if (nextCursor != null)
        {
            result.RemoveAt(result.Count - 1);
        }

        PaginatedMessagesDTO response = new PaginatedMessagesDTO
        {
            Messages = result,
            Next = nextCursor,
            HasMore = hasMore,
        };

        return response;
    }

    public async Task<(bool, string)> EditMessage(Guid callerId, Guid messageId, string newText, bool isPrivate)
    {
        try
        {
            int rows;
            if (isPrivate)
            {
                rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""PrivateMessages""
                SET Text = {newText} 
                WHERE Id = {messageId} 
                    AND SenderId = {callerId}");
            }
            else
            {
                rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""ChannelMessages""
                SET Text = {newText} 
                WHERE Id = {messageId} 
                    AND SenderId = {callerId}");
            }

            if (rows > 0)
            {
                return (true, "Successfully rejected request");
            }
            else
            {
                return (false, "Request does not exist");
            }

        }
        catch (Exception ex)
        {
            // log error
            return (false, "An internal server error occurred while sending the request.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteMessage(Guid callerId, Guid messageId, bool isPrivate)
    {
        try
        {
            int rows;
            if (isPrivate)
            {
                rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DELETE FROM ""PrivateMessages""
                    WHERE Id = {messageId} 
                        AND SenderId = {callerId}");
            }
            else
            {
                {
                    rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DELETE FROM ""ChannelMessages""
                    WHERE Id = {messageId} 
                        AND SenderId = {callerId}");
                }
            }

            if (rows > 0)
            {
                return (true, "Message deleted successfully");
            }
            else
            {
                return (false, "Message does not exist");
            }
        }
        catch (Exception ex)
        {
            return (false, "An internal server error occurred while sending the request.");

        }
    }

    public async Task<(bool Success, string ResponseMessage, MessageDTO? Message)> SendChannelMessage(Guid callerId, SendChannelMessageDTO request)
    {
        var channel = await _context.ServerChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ChannelId);
        string vagueMessage = "You are not a participant of this server, or it does not exist";

        if (channel == null)
        {
            return (false, vagueMessage, null); // We return this on purpose, so that no one can guess id
        }

        bool isParticipent = await _context.ServerParticipants.AnyAsync(p => p.ParticipantId == callerId
                            && p.ServerId == channel.ServerId);

        if (!isParticipent)
        {
            return (false, vagueMessage, null);
        }

        var senderInfo = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == callerId)
            .Select(u => new { u.Username, u.ProfilePictureUrl })
            .FirstOrDefaultAsync();

        if (senderInfo == null)
        {
            return (false, "Sender account not found.", null);
        }

        ChannelMessage message = new ChannelMessage
        {
            SenderId = callerId,
            Text = request.Message,
            ServerChannelId = request.ChannelId,
        };

        _context.ChannelMessages.Add(message);
        await _context.SaveChangesAsync();

        return (true, "Message succesfully created", new MessageDTO
        {
            Id = message.Id,
            SentAt = message.CreatedAt,
            Sender = new UserDTO 
            {
                Id = callerId,
                Username = senderInfo.Username,
                ProfilePictureUrl = senderInfo.ProfilePictureUrl,
            },
            Message = request.Message,
        }
        );
    }
}