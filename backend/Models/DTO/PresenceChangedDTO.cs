namespace backend.Models.DTO;

public class PresenceChangedDTO
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
}
