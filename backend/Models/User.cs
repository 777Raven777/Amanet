using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class User : BaseEntity
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public bool OnlyFriendsMessages { get; set; } = false;
        
        public bool AcceptInvites { get; set; } = true;

        public string? ProfilePictureUrl { get; set; }

        public DateTime LastTokenReset { get; set; } = DateTime.UtcNow;

        public DateTime? SuspendedUntil { get; set; }

        public string? SuspensionReason { get; set; }

        [Required]
        public List<TokenPermissions> Permissions { get; set; } = new List<TokenPermissions>
        {
            TokenPermissions.CanSendDirectMessages,
            TokenPermissions.CanUseServers,
            TokenPermissions.CanAddFriends,
        };
    }
}
