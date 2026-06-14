using System;
using System.Collections.Generic;

namespace AvaloniaApplication1.DTO;

public class SendMessageRequest
{
    public string Message { get; set; }

    public Guid? ReceiverId { get; set; }

    public Guid? ConversationId { get; set; }
}

public class MessageDTO
{
    public Guid Id { get; set; }
    public UserDTO Sender { get; set; }

    public string Message { get; set; }

    public DateTime SentAt { get; set; }

    public bool Edited { get; set; }
}

public class CreateOrPatchMessageDTO
{
    public string Message { get; set; }
}

public class PaginatedMessagesDTO
{
    public List<MessageDTO> Messages { get; set; }

    public Guid? Next {  get; set; }

    public bool HasMore { get; set; }
}

public class ConversationListItemDTO
{
    public Guid Id { get; set; }
    public UserDTO OtherUser { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class PaginatedConversationsDTO : PaginatedListDTO
{
    public List<ConversationListItemDTO> Conversations { get; set; }
}