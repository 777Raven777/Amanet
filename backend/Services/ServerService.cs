using backend.Data;
using backend.Models;
using backend.Models.DTO;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace backend.Services;

public class ServerService
{
    private readonly AppDbContext _context;

    public ServerService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message, ServerDTO? Server)> CreateServer(Guid callerId, string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return (false, "Server name cannot be empty.", null);
        }

        Server server = new Server
        {
            Name = serverName,
            CreatorId = callerId,
        };

        Role defaultRole = new Role
        {
            Name = "default",
            IsSystem = true,
            Actions = new List<Permissions> { Permissions.SendMessages, Permissions.DeleteMessages, Permissions.ReadMessages, Permissions.EditMessages, Permissions.InviteUsers },
            Server = server
        };

        Role adminRole = new Role
        {
            Name = "admin",
            IsSystem = true,
            Actions = new List<Permissions> { Permissions.SendMessages, Permissions.DeleteMessages, Permissions.ReadMessages, Permissions.EditMessages, Permissions.InviteUsers,
                Permissions.BanUsers, Permissions.EditUsers, Permissions.CreateChannels, Permissions.ModifyRoles },
            Server = server
        };

        ServerParticipant admin = new ServerParticipant
        {
            Server = server,
            ParticipantId = callerId,
            Role = adminRole
        };

        _context.Servers.Add(server);
        _context.Roles.Add(defaultRole);
        _context.Roles.Add(adminRole);
        _context.ServerParticipants.Add(admin);

        await _context.SaveChangesAsync();
        return (true, "Server successfully created", new ServerDTO
        {
            Name = server.Name,
            Id = server.Id,
        });
    }

    public async Task<(bool Success, string Message)> DeleteServer(Guid callerId, Guid serverId)
    {
        try
        {
            int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DELETE FROM ""Servers"" 
                WHERE Id = {serverId} AND CreatorId = {callerId}");

            if (rows > 0)
            {
                return (true, "Successfully deleted server.");
            }
            else
            {
                return (false, "Server does not exist.");
            }
        }
        catch (Exception e)
        {
            return (false, "An internal server error occurred while deleting the server.");
        }
    }

    public async Task<(bool Success, string Message, ServerDTO? Server)> EditServer(Guid callerId, Guid serverId, PatchServerDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Server name cannot be empty.", null);
        }

        int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Servers"" 
            SET Name={request.Name}
            WHERE Id={serverId} 
                AND CreatorId={callerId}");

        if (rows > 0)
        {
            return (true, "Successfully update server", new ServerDTO
            {
                Name = request.Name,
                Id = serverId,
            });
        }
        return (false, "Server with this Id does not exist", null);
    }

    public async Task<PaginatedServersListDTO> GetServersList(Guid callerId, int pageSize, int currentPage)
    {
        var servers = await _context.ServerParticipants.AsNoTracking()
            .Where(p => p.ParticipantId == callerId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ServerDTO // Do not FREAKING FORGET that select activates JOIN without include (°ロ°)
            {
                Name = p.Server.Name,
                Id = p.Server.Id,
            })
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize + 1)
            .ToListAsync();

        bool nextPage = servers.Count > pageSize;
        if (nextPage)
        {
            servers.RemoveAt(servers.Count - 1);
        }

        return new PaginatedServersListDTO
        {
            PageSize = pageSize,
            CurrentPage = currentPage,
            NextPage = nextPage,
            Servers = servers,
        };
    }
}

