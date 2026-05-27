namespace backend.Realtime;

public static class RealtimeGroups
{
    public const string ConversationPrefix = "conv-";
    public const string ChannelPrefix = "channel-";
    public const string UserPrefix = "user-";

    public static string Conversation(Guid id) => $"{ConversationPrefix}{id}";
    public static string Channel(Guid id) => $"{ChannelPrefix}{id}";
    public static string User(Guid id) => $"{UserPrefix}{id}";
}
