using backend.Models.DTO;
using backend.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ServerController : ControllerBase
{
    private readonly ServerService _service;

    public ServerController(ServerService service)
    {
        _service = service;
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPost]
    public async Task<ActionResult<ServerDTO>> CreateServer(CreateServerRequest request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, ServerDTO? server) = await _service.CreateServer(callerId, request.Name);

        if (success)
        {
            return StatusCode(201, server);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteServer(Guid id)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg) = await _service.DeleteServer(callerId, id);

        if (success)
        {
            return StatusCode(204, msg);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPatch("{id}")]
    public async Task<ActionResult> EditServer(Guid id, PatchServerDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, ServerDTO? server) = await _service.EditServer(callerId, id, request);

        if (success)
        {
            return Ok(server);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet]
    public async Task<ActionResult<PaginatedServersListDTO>> GetServersList([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        int ClampedcurrentPage = Math.Max(currentPage, 1);
        int ClampedpageSize = Math.Clamp(pageSize, 1, 50);

        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var servers = await _service.GetServersList(callerId, ClampedpageSize, ClampedcurrentPage);

        return Ok(servers);
    }
}

