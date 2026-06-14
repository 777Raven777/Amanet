using System;
using System.Collections.Generic;

namespace AvaloniaApplication1.DTO;


public class ServerDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class PaginatedServersListDTO : PaginatedListDTO
{
    public List<ServerDTO> Servers { get; set; } = new();
}

public class ServerChannelDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
