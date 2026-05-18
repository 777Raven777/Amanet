using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace backend.Models.DTO
{
    public class UserDTO
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public string Username { get; set; }
        public string ProfilePictureUrl { get; set; }
    }

    public class LoginRegisterResponse : UserDTO
    {
        public string Token { get; set; }
    }

    public class RegisterRequest {
        [Required]
        [MaxLength(255)]
        public string Email { get; set; }
        [Required]
        [MaxLength(255)]
        public string Username { get; set; }

        [Required, MaxLength(255), MinLength(8)]
        public string Password { get; set; }

        public IFormFile? ProfilePicture { get; set; }
    }

    public class LoginRequest
    {
        [Required, MaxLength(255)]
        public string EmailOrUsername { get; set; }

        [Required, MinLength(8), MaxLength(255)]
        public string Password { get; set; }
    }
}
