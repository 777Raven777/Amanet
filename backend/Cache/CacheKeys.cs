namespace backend.Cache;

public static class CacheKeys
{
    public static string User(Guid id) => $"user:{id}";
    public static string Server(Guid id) => $"server:{id}";
    public static string Role(Guid id) => $"role:{id}";
    public static string ServerParticipant(Guid id) => $"serverparticipant:{id}";
    public static string ServerChannel(Guid id) => $"serverchannel:{id}";
    public static string Relationship(Guid id) => $"relationship:{id}";
    public static string Token(string tokenValue) => $"token:{tokenValue}";
}