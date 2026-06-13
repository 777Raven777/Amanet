using backend.Cache;
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
    private readonly ICacheService _cache;

    public ServerChannelService(AppDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
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

            await _cache.RemoveAsync(CacheKeys.ServerChannels(serverId));
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
            await _cache.RemoveAsync(CacheKeys.ServerChannels(serverId));
        }

        return (true, "Channel was successfully updated", new ServerChannelDTO
        {
            Id = channel.Id,
            Name = channel.Name,
        });
    }

    public async Task<(bool Success, string Message)> DeleteServerChannel(Guid callerId, Guid serverId, Guid channelId)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.CreateChannels);

        if (!allowed)
        {
            return (false, msg);
        }

        int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DELETE FROM ""ServerChannels""
                    WHERE Id = {channelId} 
                        AND ServerId == {serverId}");

        if (rows > 0)
        {
            await _cache.RemoveAsync(CacheKeys.ServerChannels(serverId));
            return (true, "Channel was successfully deleted");
        }
        return (false, "No channel with this Id found");
    }

    public async Task<(bool Success, string Message, IEnumerable<ServerChannelDTO>? ServerChannels)> ListServerChannels(Guid callerId, Guid serverId)
    {
        (bool access, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.ReadMessages); // if not messages, then channels either

        if (!access)
        {
            return (false, msg, null);
        }

        var cachedChannels = await _cache.GetAsync<IEnumerable<ServerChannelDTO>>(CacheKeys.ServerChannels(serverId));
        if (cachedChannels != null) 
        {
            return (true, "List of channels retrieved", cachedChannels);
        }

        List<ServerChannelDTO> channels = await _context.ServerChannels.Where(c => c.ServerId == serverId)
            .Select(c => new ServerChannelDTO
            {
                Id = c.Id,
                Name = c.Name,
            })
            .ToListAsync();

        await _cache.SetAsync(CacheKeys.ServerChannels(serverId), channels, TimeSpan.FromHours(1));
        return (true, "List of channels retrieved", channels);
    }
}

