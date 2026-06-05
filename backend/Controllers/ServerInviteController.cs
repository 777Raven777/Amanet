using Microsoft.AspNetCore.Mvc;
using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/servers/{serverId}/invites")]
public class ServerInviteController : ControllerBase
{

    private readonly ServerParticipantService _service;

    public ServerInviteController(ServerParticipantService service)
    {
        _service = service;
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPost]
    public async Task<ActionResult<ServerInviteDTO?>> SendInvite(Guid serverId, InviteServerRequestDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, ServerInviteDTO? invite) = await _service.SendInvite(callerId, serverId, request.InvitedUserId);

        if (success)
        {
            return StatusCode(201, invite);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpDelete("{inviteId}")]
    public async Task<ActionResult> DeleteInvite(Guid serverId, Guid inviteId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg) = await _service.DeleteInvite(callerId, serverId, inviteId);

        if (success)
        {
            return StatusCode(204, msg);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet]
    public async Task<ActionResult<ServerInvitePaginatedDTO?>> ListServerInvites(Guid serverId, [FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        pageSize = Math.Clamp(pageSize, 1, 50);
        currentPage = Math.Clamp(currentPage, 1, int.MaxValue);

        (bool success, string msg, ServerInvitePaginatedDTO? invites) = await _service.ListServerInvites(callerId, serverId, currentPage, pageSize);

        if (success)
        {
            return Ok(invites);
        }
        return BadRequest(msg);
    }
}
