using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.Models;

[Index(nameof(TokenValue), IsUnique = true)]
public class Token : BaseEntity
{
    [Required]
    public string TokenValue { get; set; }

    [Required]
    public List<TokenPermissions> Permissions { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User User { get; set; }
}
