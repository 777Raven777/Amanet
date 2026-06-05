using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Extensions
{
    public static class AppDbContextExtensions
    {
        public static async Task<(bool Access, string Message)> VerifyUserAccessAsync(
            this AppDbContext context,
            Guid callerId,
            Guid serverId,
            Permissions permission)
        {
            var participant = await context.ServerParticipants
                .Include(sp => sp.Role)
                .FirstOrDefaultAsync(sp => sp.ServerId == serverId && sp.ParticipantId == callerId);

            if (participant == null)
            {
                return (false, "Either you are not a part of the server, or server does not exist");
            }

            if (!participant.Role.Actions.Contains(permission))
            {
                return (false, "You do not have correct permissions, maybe try sudo");
            }

            return (true, string.Empty);
        }
    }
}
