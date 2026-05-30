namespace backend.Models
{
    public class Conversation : BaseEntity
    {
        public Guid UserLowId { get; set; }
        public User UserLow { get; set; }

        public Guid UserHighId { get; set; }
        public User UserHigh { get; set; }
    }
}