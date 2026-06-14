using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ServerParticipantService
{
    private readonly AppDbContext _context;

    public ServerParticipantService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message, ServerInviteDTO? Invite)> SendInvite(Guid callerId, Guid serverId, Guid invitedId)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.InviteUsers);
        if (!allowed)
        {
            return (false, msg, null);
        }

        var targetUser = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == invitedId)
                .Select(u => new
                {
                    u.Username,
                    IsParticipant = _context.ServerParticipants.Any(p => p.ServerId == serverId && p.ParticipantId == invitedId),
                    IsAlreadyInvited = _context.ServerInvites.Any(i => i.ServerId == serverId && i.InvitedUserId == invitedId)
                })
                .FirstOrDefaultAsync();

        if (targetUser == null)
        {
            return (false, "Target user does not exist", null);
        }
        if (targetUser.IsParticipant)
        {
            return (false, "User is already a participant in the server", null);
        }
        if (targetUser.IsAlreadyInvited)
        {
            return (false, "User is already invited", null);
        }

        ServerInvite invite = new ServerInvite
        {
            ServerId = serverId,
            InviterId = callerId,
            InvitedUserId = invitedId,
            Status = RelationshipType.Waiting,
        };

        _context.ServerInvites.Add(invite);
        await _context.SaveChangesAsync();

        return (true, "User invited to the server successfully", new ServerInviteDTO
        {
            Id = invite.Id,
            InvitedUsername = targetUser.Username
        });
    }

    public async Task<(bool Success, string Message)> AcceptInvite(Guid callerId, Guid inviteId)
    {
        // AUTOMATICALLY delete on accept
        var invite = await _context.ServerInvites
            .Include(i => i.InvitedUser) 
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.InvitedUserId == callerId);

        if (invite == null)
        {
            return (false, "Invite not found or you don't have permission to accept it.");
        }

        _context.ServerInvites.Remove(invite);

        var defaultRole = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.ServerId == invite.ServerId &&
                        r.IsSystem == true &&
                        r.Name == "default");

        if (defaultRole == null) 
        {
            return (false, "Server does not have default role");
        }

        var newParticipant = new ServerParticipant
        {
            ServerId = invite.ServerId,
            ParticipantId = callerId,
            RoleId = defaultRole.Id,
            CustomName = invite.InvitedUser.Username,
        };

        _context.ServerParticipants.Add(newParticipant);
        await _context.SaveChangesAsync();
        return (true, "Invite accepted");
    }

    public async Task<(bool Success, string Message)> RejectInvite(Guid callerId, Guid inviteId)
    {
        int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DELETE FROM ""ServerInvites""
                    WHERE ""Id"" = {inviteId} AND ""InvitedUserId"" = {callerId}");

        if (rows > 0)
        {
            return (true, "Server invite was rejected and removed");
        }
        return (false, "Server Invite with this Id was not found");
    }

    public async Task<(bool Success, string Message)> DeleteInvite(Guid callerId, Guid serverId, Guid inviteId)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.InviteUsers);

        if (!allowed)
        {
            return (false, msg);
        }

        int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DELETE FROM ""ServerInvites""
                    WHERE ""Id"" = {inviteId} AND ""InviterId"" = {callerId}");

        if (rows > 0)
        {
            return (true, "Server invite was deleted");
        }
        return (false, "Server Invite with this Id was not found");
    }

    public async Task<(bool Success, string Message, ServerInvitePaginatedDTO? Invites)> ListServerInvites(Guid callerId, Guid serverId, int currentPage, int pageSize)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.InviteUsers);

        if (!allowed)
        {
            return (false, msg, null);
        }

        var invites = await _context.ServerInvites
            .Where(i => i.ServerId == serverId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ServerInviteDTO
            {
                Id = i.Id,
                InvitedUsername = i.InvitedUser.Username,
            })
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize + 1)
            .ToListAsync();

        bool nextPage = invites.Count > pageSize;
        if (nextPage)
        {
            invites.RemoveAt(invites.Count);
        }

        return (true, "Successfully retrieved invites", new ServerInvitePaginatedDTO
        {
            PageSize = pageSize,
            CurrentPage = currentPage,
            NextPage = nextPage,
            InvitesList = invites,
        });
    }

    public async Task<(bool Success, string Message, ReceivedServerInvitePaginatedDTO? Invites)> ListReceivedInvites(Guid callerId, int currentPage, int pageSize)
    {
        var invites = await _context.ServerInvites
            .Where(i => i.InvitedUserId == callerId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ReceivedServerInviteDTO
            {
                Id = i.Id,
                ServerName = i.Server.Name,
            })
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize + 1)
            .ToListAsync();

        bool nextPage = invites.Count > pageSize;
        if (nextPage)
        {
            invites.RemoveAt(invites.Count);
        }

        return (true, "Successfully retrieved invites", new ReceivedServerInvitePaginatedDTO
        {
            PageSize = pageSize,
            CurrentPage = currentPage,
            NextPage = nextPage,
            ReceivedInvitesList = invites,
        });
    }

    public async Task<(bool Success, string Message, ServerParticipantDTO? Participant)> ModifyParticipant(Guid callerId, Guid serverId, Guid participantId, PatchParticipantDTO request)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.EditUsers);

        if (!allowed)
        {
            return (false, msg, null);
        }

        var participant = await _context.ServerParticipants
                .FirstOrDefaultAsync(p => p.ServerId == serverId && p.ParticipantId == participantId);

        if (participant == null)
        {
            return (false, "User does not exist in this server", null);
        }

        bool wasModified = false;
        if (request.RoleId.HasValue)
        {
            bool roleValid = await _context.Roles
                .AnyAsync(r => r.Id == request.RoleId.Value && r.ServerId == serverId);

            if (!roleValid)
            {
                return (false, "Invalid Role ID, or Role does not belong to this server", null);
            }

            participant.RoleId = request.RoleId.Value;
            wasModified = true;
        }
        if (!String.IsNullOrEmpty(request.CustomName) && !String.IsNullOrWhiteSpace(request.CustomName))
        {
            participant.CustomName = request.CustomName;
            wasModified = true;
        }
        if (wasModified)
        {
            await _context.SaveChangesAsync();
            return (true, "User data was successfully modified", null);
        }

        return (false, "User does not exist", null);
    }

    public async Task<(bool Success, string Message, ServerParticipantPaginatedDTO? Participants)> ListParticipants(Guid callerId, Guid serverId, int currentPage, int pageSize)
    {
        (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.ReadMessages);

        if (!allowed)
        {
            return (false, msg, null);
        }

        var participants = await _context.ServerParticipants
            .Where(i => i.ServerId == serverId && i.ParticipantId != callerId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ServerParticipantDTO
            {
                Id = i.ParticipantId,
                Username = i.CustomName,
                ProfilePictureUrl = i.Participant.ProfilePictureUrl,
                RoleName = i.Role.Name,
            })
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize + 1)
            .ToListAsync();

        bool nextPage = participants.Count > pageSize;
        if (nextPage)
        {
            participants.RemoveAt(participants.Count);
        }

        return (true, "Successfully retrieved invites", new ServerParticipantPaginatedDTO
        {
            PageSize = pageSize,
            CurrentPage = currentPage,
            NextPage = nextPage,
            Participants = participants,
        });
    }

    public async Task<(bool Success, string Message)> DeleteParticipant(Guid callerId, Guid serverId, Guid participantId)
    {
        var participant = await _context.ServerParticipants
            .Include(p => p.Server)
            .FirstOrDefaultAsync(p => p.ParticipantId == participantId && p.ServerId == serverId);

        if (participant == null)
        {
            return (false, "Server Invite with this Id was not found");
        }

        if (participant.ParticipantId != callerId)
        {
            (bool allowed, string msg) = await _context.VerifyUserAccessAsync(callerId, serverId, Permissions.BanUsers);

            if (!allowed) return (false, msg);
        }

        if (participant.Server.CreatorId == participantId)
        {
            return (false, "You cannot remove the creator of the server.");
        }

        _context.ServerParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        return (true, "Participant was successfully removed from the server.");
    }
}
