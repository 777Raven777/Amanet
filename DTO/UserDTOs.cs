using System;
using System.Collections.Generic;

namespace AvaloniaApplication1.DTO;

public class UserDTO
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
}

public class MeDTO : UserDTO
{
    public string Email { get; set; }
}

public class LoginRegisterResponse : UserDTO
{
    public string Token { get; set; } = string.Empty;
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedUntil { get; set; }
}

public class LoginRequest
{
    public string EmailOrUsername { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
public class PaginatedUserListDTO : PaginatedListDTO
{
    public List<UserDTO> Users { get; set; }
}