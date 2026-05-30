namespace backend.Models
{
    public class Server : BaseEntity
    {
        public string Name { get; set; }
        public Guid CreatorId { get; set; }
        public User Creator { get; set; }
    }
}
