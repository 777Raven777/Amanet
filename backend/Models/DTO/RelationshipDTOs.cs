using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

namespace backend.Models.DTO
{
    public class RelationshipDTO
    {
        [Required]
        public Guid Id { get; set; }

        public UserDTO? Sender { get; set; }

        public UserDTO? Receiver { get; set; }

        [Required]
        public RelationshipType Status { get; set; }
    }

    public class FriendDTO
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public UserDTO Friend { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }
    }

    public class SendRequestDTO
    {
        [Required]
        public string Id { get; set; }
    }
}
