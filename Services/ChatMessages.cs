using AvaloniaApplication1.DTO;
using System;

namespace AvaloniaApplication1.Services;

// What you send up the socket. Type is "dm" or "channel".
// Adjust property names/casing to match exactly what your backend expects.
public class OutgoingChatMessage
{
    public string Type { get; set; } = "dm";

    // for "dm"
    public Guid? ConversationId { get; set; }
    public Guid? ReceiverId { get; set; }   // optional: start a new DM

    // for "channel"
    public Guid? ServerId { get; set; }
    public Guid? ChannelId { get; set; }

    public string Message { get; set; } = string.Empty;
}

// What the server pushes down the socket.
public class IncomingChatMessage
{
    public string Type { get; set; } = "dm";

    public Guid? ConversationId { get; set; }
    public Guid? ServerId { get; set; }
    public Guid? ChannelId { get; set; }

    public MessageDTO Message { get; set; } = default!;
}

// Broadcast to view models when a frame arrives (already on the UI thread).
public sealed record ChatMessageReceived(IncomingChatMessage Message);
