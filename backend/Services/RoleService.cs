using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Models.DTO;
using Microsoft.EntityFrameworkCore;
namespace backend.Services;

public class RoleService
{
    private readonly AppDbContext _context;

    public RoleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message, RoleDTO? Role)> CreateRole(Guid callerId, Guid serverId, CreateOrPatchRoleDTO request)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.ModifyRoles);

        if (!allowed)
        {
            return (false, msg, null);
        }

        int roleCount = await _context.Roles.CountAsync(r => r.ServerId == serverId);
        // Unlike in server channels creation here we dont use transactions and locks for expiriment puproses, in prod must be changed 100%
        if (roleCount > 50)
        {
            return (false, "Each server can have at max 50 roles", null);
        }

        Role role = new Role
        {
            Name = request.Name,
            Actions = request.Actions,
            ServerId = serverId,
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        return (true, "Role was successfully created", new RoleDTO
        {
            Name = request.Name,
            Actions = request.Actions,
            Id = role.Id,
        });
    }
}

