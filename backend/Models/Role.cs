namespace backend.Models
{
    public class Role : BaseEntity
    {
        public string Name { get; set; }
        public List<Permissions> Actions { get; set; }
        public Server server { get; set; }
        public Guid ServerId { get; set; }
        public bool IsSystem { get; set; }
    }
}
