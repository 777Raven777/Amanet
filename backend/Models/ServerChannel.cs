namespace backend.Models
{
    public class ServerChannel : BaseEntity
    {
        public string Name { get; set; }
        public Guid ServerId { get; set; }
        public Server Server { get; set; }
    }
}
