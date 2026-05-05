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

        [Required]
        public UserDTO Sender { get; set; }

        [Required]
        public UserDTO Receiver { get; set; }

        [Required]
        public RelationshipType Status { get; set; }
    }

    public class WaitingRelationshipsDTO
    {
        [Required]
        public List<RelationshipDTO> Sent { get; set; }
        [Required]
        public List<RelationshipDTO> Received { get; set; }
    }
}
