using System;
using System.Collections.Generic;

namespace AvaloniaApplication1.DTO;

public enum Permissions
{
    SendMessages = 0,
    DeleteMessages = 1,
    BanUsers = 2,
    ReadMessages = 3,
    EditMessages = 4,
    InviteUsers = 5,
    EditUsers = 6,
    CreateChannels = 7,
    ModifyRoles = 8,
}

public class RoleDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<Permissions>? Actions { get; set; }
    public bool IsSystem { get; set; }
}