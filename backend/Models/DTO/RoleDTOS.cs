namespace backend.Models.DTO;

public class RoleDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<Permissions>? Actions { get; set; }

    public bool IsSystem { get; set; }
}

public class CreateOrPatchRoleDTO
{
    public string? Name { get; set; }
    public List<Permissions>? Actions { get; set; }
}


