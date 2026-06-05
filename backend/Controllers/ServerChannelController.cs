using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/servers/{serverId}/channels")]
public class ServerChannelController : ControllerBase
{
    private readonly ServerChannelService _service;

    public ServerChannelController(ServerChannelService service)
    {
        _service = service;
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPost]
    public async Task<ActionResult<ServerChannelDTO>> CreateServerChannel(Guid serverId, CreateOrPatchChannelDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, ServerChannelDTO? serverChannel) = await _service.CreateServerChannel(callerId, serverId, request);

        if (success)
        {
            return StatusCode(201, serverChannel);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpDelete("{channelId}")]
    public async Task<ActionResult> DeleteServerChannel(Guid serverId, Guid channelId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg) = await _service.DeleteServerChannel(callerId, serverId, channelId);

        if (success)
        {
            return StatusCode(204, msg);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPatch("{channelId}")]
    public async Task<ActionResult<ServerChannelDTO>> EditServerChannel(Guid serverId, Guid channelId, CreateOrPatchChannelDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, ServerChannelDTO serverChannel) = await _service.EditServerChannel(callerId, serverId, channelId, request);

        if (success)
        {
            return Ok(serverChannel);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServerChannelDTO?>>> ListServerChannels(Guid serverId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, IEnumerable<ServerChannelDTO>? serverChannels) = await _service.ListServerChannels(callerId, serverId);

        if (success)
        {
            return Ok(serverChannels);
        }
        return BadRequest(msg);
    }
}