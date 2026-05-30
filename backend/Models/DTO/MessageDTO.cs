using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTO;

public class MessageDTO
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid SenderId { get; set; }

    public string? SenderUsername { get; set; }
    public string? SenderProfilePictureUrl { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; }

    public bool Edited { get; set; }
}

public class PrivateMessageDTO : MessageDTO
{
    [Required]
    public Guid ConversationId { get; set; }
}

public class ChannelMessageDTO : MessageDTO
{
    [Required]
    public Guid ChannelId { get; set; }
}

public class PresenceChangedDTO
{
    [Required]
    public Guid UserId { get; set; }


    [Required]
    public string Status { get; set; } = "offline";
}
