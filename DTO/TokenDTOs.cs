//using System;
//using System.ComponentModel.DataAnnotations;

//namespace AvaloniaApplication1.DTO;

//public class TokenDTO
//{
//    [Required]
//    public string TokenValue { get; set; }

//    [Required]
//    public Guid UserId { get; set; }

//    [Required]
//    public List<TokenPermissions> Permissions { get; set; }
//}

//public class SuspendedTokenDTO : TokenDTO
//{
//    public DateTime? SuspensionEndsAt { get; set; }
//    public string? SuspensionReason { get; set; }
    
//}