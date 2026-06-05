using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Models.DTO;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
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

        if (String.IsNullOrEmpty(request.Name) || String.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Name of the role cannot be empty", null);
        }

        Role role = new Role
        {
            Name = request.Name,
            Actions = request.Actions,
            ServerId = serverId,
            IsSystem = false,
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        return (true, "Role was successfully created", new RoleDTO
        {
            Name = request.Name,
            Actions = request.Actions,
            Id = role.Id,
            IsSystem = false,
        });
    }

    public async Task<(bool Success, string  Message)> DeleteRole(Guid callerId, Guid serverId, Guid roleId)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.ModifyRoles);

        if (!allowed) return (false, msg);

        var roleToDelete = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.ServerId == serverId);

        if (roleToDelete == null || roleToDelete.IsSystem)
        {
            return (false, "No role with that Id found, or the role is a system role.");
        }

        var defaultRole = await _context.Roles.FirstOrDefaultAsync(r =>
                        r.ServerId == serverId &&
                        r.IsSystem == true &&
                        r.Name == "default");

        if (defaultRole == null)
        {
            return (false, "Critical Error: Default system role not found for this server.");
        }

        var affectedParticipants = await _context.ServerParticipants
                                .Where(p => p.ServerId == serverId && p.RoleId == roleId)
                                .ToListAsync();

        foreach (var participant in affectedParticipants)
        {
            participant.RoleId = defaultRole.Id;
        }
        _context.Roles.Remove(roleToDelete);

        await _context.SaveChangesAsync();

        return (true, "Role was successfully deleted and users were reassigned to the default role.");
    }

    public async Task<(bool Success, string Message, RoleDTO? Role)> PatchRole(Guid callerId, Guid serverId, Guid roleId, CreateOrPatchRoleDTO request)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.ModifyRoles);

        if (!allowed)
        {
            return (false, msg, null);
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.ServerId == serverId);

        if (role == null)
        {
            return (false, "Role with this id does not exist", null);
        } 
        if (role.IsSystem)
        {
            return (false, "Cannot modify system role", null);
        }

        bool updated = false;

        if (!String.IsNullOrEmpty(request.Name) && !String.IsNullOrWhiteSpace(request.Name)) { role.Name = request.Name; updated = true; }
        if (request.Actions != null && request.Actions.Count() > 0) { role.Actions = request.Actions; updated = true; }

        if (updated)
        {
            await _context.SaveChangesAsync();
        }

        return (true, "Role was successfully patched", new RoleDTO
        {
            Name = role.Name,
            Id = role.Id,
            Actions = role.Actions,
            IsSystem = role.IsSystem,
        });
    }

    public async Task<(bool Success, string Message, IEnumerable<RoleDTO>? Roles)> ListRoles(Guid callerId, Guid serverId)
    {
        bool isParticipant = await _context.ServerParticipants.AnyAsync(p => p.ParticipantId == callerId && p.ServerId == serverId);

        if (!isParticipant)
        {
            return (false, "No server with this Id exists", null);
        }

        List<RoleDTO> roles = await _context.Roles.Where(r => r.ServerId == serverId)
            .Select(r => new RoleDTO
            {
                Id = r.Id,
                Name = r.Name,
                IsSystem = r.IsSystem,
            })
            .ToListAsync();

        return (true, "List of roles retrieved", roles);
    }

    public async Task<(bool Success, string Message, RoleDTO? Role)> RetrieveRole(Guid callerId, Guid serverId, Guid roleId)
    {
        bool isParticipant = await _context.ServerParticipants.AnyAsync(p => p.ParticipantId == callerId && p.ServerId == serverId);

        if (!isParticipant)
        {
            return (false, "No server with this Id exists", null);
        }
        var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId && r.ServerId == serverId);

        if (role == null)
        {
            return (false, "No role with this Id found", null);
        }
        return (true, "Role retrieved", new RoleDTO
        {
            Name = role.Name,
            Id = role.Id,
            Actions = role.Actions,
            IsSystem = role.IsSystem,
        });
    }
}

