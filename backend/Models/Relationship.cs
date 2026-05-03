namespace backend.Models
{
    public class Relationship : BaseEntity
    {
        public Guid SenderId { get; set; }
        public User Sender { get; set; }

        public Guid ReceiverId { get; set; }
        public User Receiver { get; set; }

        public RelationshipType Status { get; set; }
    }
}
