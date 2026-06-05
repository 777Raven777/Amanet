using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTO;

public class ServerInviteDTO
{
    public Guid Id { get; set; }

    public string InvitedUsername { get; set; }
}

public class InviteServerRequestDTO
{
    [Required]
    public Guid InvitedUserId { get; set; }
}

public class ReceivedServerInviteDTO
{
    public Guid Id { get; set; }
    public string ServerName { get; set; }
}

public class ServerInvitePaginatedDTO : PaginatedListDTO
{
    public List<ServerInviteDTO>? InvitesList { get; set; }
}

public class ReceivedServerInvitePaginatedDTO : PaginatedListDTO
{
    public List<ReceivedServerInviteDTO>? ReceivedInvitesList { get; set; }
}

public class ServerParticipantDTO : UserDTO
{
    public string RoleName { get; set; }
}

public class ServerParticipantPaginatedDTO : PaginatedListDTO
{
    public List<ServerParticipantDTO>? Participants { get; set; }
}

public class PatchParticipantDTO
{
    public Guid? RoleId { get; set; }
    public string? CustomName { get; set; }
}
