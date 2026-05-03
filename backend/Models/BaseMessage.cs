using System.ComponentModel.DataAnnotations;
namespace backend.Models
{
    public class BaseMessage : BaseEntity
    {
        public Guid SenderId { get; set; }
        public User Sender { get; set; }

        [Required]
        public string Text { get; set; }
        public bool Edited { get; set; } = false;

    }
}
