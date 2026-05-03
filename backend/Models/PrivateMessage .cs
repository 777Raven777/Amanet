using Microsoft.VisualBasic;

namespace backend.Models
{
    public class PrivateMessage : BaseMessage
    {
        public Guid ConversationId { get; set; }
        public Conversation Conversation { get; set; }
    }
}
