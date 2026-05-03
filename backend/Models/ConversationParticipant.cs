namespace backend.Models
{
    public class ConversationParticipant : BaseEntity
    {
        public Guid ConversationId { get; set; }
        public Conversation Conversation { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}
