using Microsoft.AspNetCore.Mvc;
using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/servers/{serverId}/participants")]
public class ServerParticipantController : ControllerBase
{
    private readonly ServerParticipantService _service;

    public ServerParticipantController ( ServerParticipantService service ) 
    {
        _service = service;
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPatch("{participantId}")]
    public async Task<ActionResult<ServerParticipantDTO?>> ModifyParticipant(Guid serverId, Guid participantId, PatchParticipantDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, ServerParticipantDTO? participant) = await _service.ModifyParticipant(callerId, serverId, participantId, request);

        if (success)
        {
            return Ok(participant);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpDelete("{participantId}")]
    public async Task<ActionResult> BanUserOrLeave(Guid serverId, Guid participantId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg) = await _service.DeleteParticipant(callerId, serverId, participantId);

        if (success)
        {
            return StatusCode(204, msg);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet]
    public async Task<ActionResult<ServerParticipantPaginatedDTO?>> ListServerInvites(Guid serverId, [FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        pageSize = Math.Clamp(pageSize, 1, 50);
        currentPage = Math.Clamp(currentPage, 1, int.MaxValue);

        (bool success, string msg, ServerParticipantPaginatedDTO? participants) = await _service.ListParticipants(callerId, serverId, currentPage, pageSize);

        if (success)
        {
            return Ok(participants);
        }
        return BadRequest(msg);
    }
}

