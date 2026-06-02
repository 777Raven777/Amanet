using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTO;

public class SendMessageRequest
{
    [Required(ErrorMessage = "Message is required.")]
    [MaxLength(4096)]
    public string Message { get; set; }

    public Guid? ReceiverId { get; set; }

    public Guid? ConversationId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            yield return new ValidationResult(
                "You cannot send empty messages",
                new[] { nameof(Message) }
            );
        }

        if (ConversationId == null && ReceiverId == null)
        {
            yield return new ValidationResult(
                "Either provide ConversationId or ReceiverId",
                new[] { nameof(ConversationId), nameof(ReceiverId) }
            );
        }
    }
}

public class MessageDTO
{
    public Guid Id { get; set; }
    public UserDTO Sender { get; set; }

    [Required(ErrorMessage = "Message is required.")]
    [MaxLength(4096)]
    public string Message { get; set; }

    public DateTime SentAt { get; set; }

    public bool Edited { get; set; }
}

public class CreateOrPatchMessageDTO
{
    [Required(ErrorMessage = "Message is required.")]
    [MaxLength(4096)]
    public string Message { get; set; }
}

public class PaginatedMessagesDTO
{
    public List<MessageDTO> Messages { get; set; }

    public Guid? Next {  get; set; }

    public bool HasMore { get; set; }
}