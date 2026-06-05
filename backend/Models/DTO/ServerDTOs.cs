using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTO;
public class ServerDTO
{
    [Required]
    public string Name { get; set; }

    [Required]
    public Guid Id { get; set; }
}

public class CreateServerRequest
{
    [Required]
    [MaxLength(120), MinLength(1)]
    public string Name { get; set; }
}

public class PatchServerDTO
{
    // Class that must be updated each time new fields are added that can be edited
    [Required]
    [MaxLength(120), MinLength(1)]
    public string Name { get; set; }
}

public class ServerChannelDTO
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(120), MinLength(1)]
    public string Name { get; set; }
}

public class CreateOrPatchChannelDTO
{
    [MaxLength(120), MinLength(1)]
    public string? Name { get; set; }
}

public class PaginatedServersListDTO : PaginatedListDTO
{

    public List<ServerDTO> Servers { get; set; }

}