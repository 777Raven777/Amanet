using AvaloniaApplication1.DTO;

namespace AvaloniaApplication1.Services;

public sealed record ChannelMessageReceived(MessageDTO Message);
public sealed record PrivateMessageReceived(MessageDTO Message);