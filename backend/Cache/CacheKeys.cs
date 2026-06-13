namespace backend.Cache;

public static class CacheKeys
{
    public static string User(Guid id) => $"user:{id}";
    public static string Server(Guid id) => $"server:{id}";
    public static string Roles(Guid serverId) => $"roles:{serverId}";
    public static string ServerParticipant(Guid id) => $"serverparticipant:{id}";
    public static string ServerChannels(Guid serverId) => $"serverchannels:{serverId}";
    public static string Relationship(Guid id) => $"relationship:{id}";
    public static string Token(string tokenValue) => $"token:{tokenValue}";
}