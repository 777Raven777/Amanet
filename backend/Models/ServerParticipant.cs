using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace backend.Models
{
    public class ServerParticipant : BaseEntity
    {
        public Guid ServerId  { get; set; }
        public Server Server { get; set; }
        public User Participant { get; set; }
        public Role Role { get; set; }
        public string CustomName { get; set; }
    }
}
