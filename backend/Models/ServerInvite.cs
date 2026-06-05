namespace backend.Models;

public class ServerInvite : BaseEntity
{
    public Guid ServerId { get; set; }
    public Server Server { get; set; }

    public Guid InviterId { get; set; }
    public User Inviter { get; set; }

    public Guid InvitedUserId { get; set; }
    public User InvitedUser { get; set; }

    public RelationshipType Status { get; set; }
}
