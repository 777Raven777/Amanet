using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace backend.Services;

public class ServerChannelService
{
    private readonly AppDbContext _context;

    public ServerChannelService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message, ServerChannelDTO? ServerChannel)> CreateServerChannel(Guid callerId, Guid serverId, CreateOrPatchChannelDTO request)
    {
        try
        {
            (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.CreateChannels);

            if (!allowed)
            {
                return (false, msg, null);
            }

            // As wise man (Michael Stonebraker) told once: transactions must be as short as possible
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // We here lock our server row to omit possibility of multiple channel creations,
            // buuuutt it is overengineered for this type of operation, so worth reviewing later
            await _context.Database.ExecuteSqlInterpolatedAsync(
                    $@"
                    SELECT 1
                    FROM ""Servers""
                    WHERE ""Id"" = {serverId}
                    FOR UPDATE NOWAIT");

            int numberOfExistingChannels = await _context.ServerChannels.CountAsync(c => c.ServerId == serverId);

            if (numberOfExistingChannels >= 30)
            {
                return (false, "Server cannot have more than 30 channels", null);
            }

            ServerChannel channel = new ServerChannel
            {
                Name = request.Name,
                ServerId = serverId,
            };

            _context.ServerChannels.Add(channel);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return (true, "Channel was successfully created", new ServerChannelDTO
            {
                Name = channel.Name,
                Id = channel.Id,
            });
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return (
                false,
                "Another operation on this server is currently in progress",
                null
            );
        }
        catch (Exception ex) 
        {
            // Also catches errors if someone deletes server during operation, we wont catch it, for now at elast
            return (false, "During channel creation server error occured", null);
        }
    }

    public async Task<(bool Success, string Message, ServerChannelDTO? ServerChannel)> EditServerChannel(Guid callerId, Guid serverId, Guid channelId, CreateOrPatchChannelDTO request)
    {
        var channel = await _context.ServerChannels.FirstOrDefaultAsync(c => c.Id == channelId && c.ServerId == serverId);

        if (channel == null) 
        {
            return (false, "Either you are not a part of the server, or server does not exist", null);
        }

        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.CreateChannels);

        if (!allowed)
        {
            return (false, msg, null);
        }

        bool isUpdated = false;

        if (!string.IsNullOrWhiteSpace(request.Name) && channel.Name != request.Name)
        {
            channel.Name = request.Name;
            isUpdated = true;
        }

        if (isUpdated)
        {
            await _context.SaveChangesAsync();
        }

        return (true, "Channel was successfully updated", new ServerChannelDTO
        {
            Id = channel.Id,
            Name = channel.Name,
        });
    }

    public async Task<(bool Success, string Message)> DeleteServerChannel(Guid callerId, Guid serverId, Guid channelId)
    {
        var channel = await _context.ServerChannels.FirstOrDefaultAsync(c => c.Id == channelId && c.ServerId == serverId);

        if (channel == null)
        {
            return (false, "Either you are not a part of the server, or server does not exist");
        }

        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.CreateChannels);

        if (!allowed)
        {
            return (false, msg);
        }

        _context.ServerChannels.Remove(channel);
        await _context.SaveChangesAsync();
        return (true, "Channel was successfully removed");
    }

    public async Task<(bool Success, string Message, IEnumerable<ServerChannelDTO>? ServerChannels)> ListServerChannels(Guid callerId, Guid serverId)
    {
        (bool access, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.ReadMessages); // if not messages, then channels either

        if (!access)
        {
            return (false, msg, null);
        }

        List<ServerChannelDTO> channels = await _context.ServerChannels.Where(c => c.ServerId == serverId)
            .Select(c => new ServerChannelDTO
            {
                Id = c.Id,
                Name = c.Name,
            })
            .ToListAsync();

        return (true, "List of channels retrieved", channels);
    }
}

