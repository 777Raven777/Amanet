namespace backend.Models
{
    public class ChannelMessage : BaseMessage
    {
        public Guid ServerChannelId { get; set; }
        public ServerChannel ServerChannel { get; set; }
    }
}
