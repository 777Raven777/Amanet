using System;

namespace AvaloniaApplication1.ViewModels;

// Client-side display models. Shapes deliberately mirror the backend DTOs
// so mapping is 1:1 once the HTTP layer lands:
//   FriendItem        <- FriendDTO        (GET api/v1/Friends/friends-list)
//   FriendRequestItem <- RelationshipDTO  (GET api/v1/Friends/requests/{received|sent})
//   ServerInviteItem  <- ReceivedServerInviteDTO (GET api/v1/User/server-invites)

public sealed class FriendItem
{
    public required Guid RelationshipId { get; init; }   // FriendDTO.Id — needed for DELETE api/v1/Friends/{id}
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public DateTime Since { get; init; }

    public string Initial => Username.Length > 0 ? Username[..1].ToUpperInvariant() : "?";
    public string SinceText => $"allied since {Since:dd MMM yyyy}";
}

public sealed class FriendRequestItem
{
    public required Guid RelationshipId { get; init; }   // RelationshipDTO.Id — accept/reject/delete all key on it
    public required string Username { get; init; }
    public string? ProfilePictureUrl { get; init; }

    public string Initial => Username.Length > 0 ? Username[..1].ToUpperInvariant() : "?";
}

public sealed class ServerInviteItem
{
    public required Guid InviteId { get; init; }          // ReceivedServerInviteDTO.Id
    public required string ServerName { get; init; }
}

public sealed class UserSearchResult
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public string Initial => Username.Length > 0 ? Username[..1].ToUpperInvariant() : "?";
}