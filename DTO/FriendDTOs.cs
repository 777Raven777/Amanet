using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvaloniaApplication1.DTO;

public class RelationshipUserDTO
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string? ProfilePictureUrl { get; set; }
}

public class FriendDTO
{
    public Guid Id { get; set; }
    public RelationshipUserDTO Friend { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class RelationshipDTO
{
    public Guid Id { get; set; }
    public RelationshipUserDTO? Sender { get; set; }
    public RelationshipUserDTO? Receiver { get; set; }
}

public class PaginatedListDTO
{
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }
    public bool NextPage { get; set; }
}

public class PaginatedRequestListDTO : PaginatedListDTO
{
    public List<RelationshipDTO> Relationships { get; set; } = new();
}