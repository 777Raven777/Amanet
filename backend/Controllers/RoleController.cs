using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/servers/{serverId}/roles")]
public class RoleController : ControllerBase
{
    private readonly RoleService _service;

    public RoleController(RoleService service)
    {
        _service = service;
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPost]
    public async Task<ActionResult<RoleDTO?>> CreateRole(Guid serverId, CreateOrPatchRoleDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, RoleDTO? role) = await _service.CreateRole(callerId, serverId, request);

        if (success)
        {
            return StatusCode(201, role);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpDelete("{roleId}")]
    public async Task<ActionResult> DeleteRole(Guid serverId, Guid roleId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg) = await _service.DeleteRole(callerId, serverId, roleId);

        if (success)
        {
            return StatusCode(204, msg);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPatch("{roleId}")]
    public async Task<ActionResult<RoleDTO?>> PatchRole(Guid serverId, Guid roleId, CreateOrPatchRoleDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, RoleDTO? role) = await _service.PatchRole(callerId, serverId, roleId, request);

        if (success)
        {
            return Ok(role);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDTO>?>> ListRoles(Guid serverId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, IEnumerable<RoleDTO>? roles) = await _service.ListRoles(callerId, serverId);

        if (success)
        {
            return Ok(roles);
        }
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet("{roleId}")]
    public async Task<ActionResult<RoleDTO?>> RetrieveRole(Guid serverId, Guid roleId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string msg, RoleDTO? role) = await _service.RetrieveRole(callerId, serverId, roleId);

        if (success)
        {
            return Ok(role);
        }
        return BadRequest(msg);
    }
}
    