using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MessageController : ControllerBase
{
    private readonly MessageService _service;

    public MessageController(MessageService service)
    {
        _service = service;
    }

    [Authorize(Policy="CanSendDirectMessages")]
    [HttpPost("dms/send-message")]
    public async Task<ActionResult<MessageDTO>> SendPrivateMessage(SendMessageRequest request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText, MessageDTO? message) = await _service.SendPrivateMessage(callerId, request);

        if (success)
        {
            return StatusCode(201, message);
        }
        return BadRequest(responseText);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPost("servers/{serverId}/server-channels/{channelId}/send-message")]
    public async Task<ActionResult<MessageDTO>> SendChannelMessage(Guid serverId, Guid channelId, CreateOrPatchMessageDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText, MessageDTO? message) = await _service.SendChannelMessage(callerId, serverId, channelId, request);

        if (success)
        {
            return StatusCode(201, message);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpGet("dms/{conversationId}/get-messages")]
    public async Task<ActionResult<PaginatedMessagesDTO>> GetPrivateMessages(Guid conversationId, [FromQuery] Guid? cursor = null, [FromQuery] int pageSize = 20)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        pageSize = Math.Clamp(pageSize, 1, 100);

        (bool success, string responseText, PaginatedMessagesDTO? messages) = await _service.GetPrivateMessages(callerId, conversationId, pageSize, cursor);

        if (success)
        {
            return Ok(messages);
        }
        return BadRequest(responseText);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpGet("servers/{serverId}/server-channels/{channelId}/get-messages")]
    public async Task<ActionResult<PaginatedMessagesDTO>> GetServerChannelMessages(Guid serverId, Guid channelId, [FromQuery] Guid? cursor = null, [FromQuery] int pageSize = 20)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        pageSize = Math.Clamp(pageSize, 1, 100);

        (bool success, string responseText, PaginatedMessagesDTO? messages) = await _service.GetServerChannelMessages(callerId, serverId, channelId, pageSize, cursor);

        if (success)
        {
            return Ok(messages);
        }
        return BadRequest(responseText);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPatch("servers/{serverId}/server-channels/{messageId}")]
    public async Task<ActionResult> PatchChannelMessage(Guid serverId, Guid messageId, CreateOrPatchMessageDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.EditChannelMessage(callerId, messageId, serverId, request.Message);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize(Policy = "CanSendDirectMessages")]
    [HttpPatch("dms/{messageId}")]
    public async Task<ActionResult> PatchPrivateMessage(Guid messageId, CreateOrPatchMessageDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.EditPrivateMessage(callerId, messageId, request.Message);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpDelete("servers/{serverId}/server-channels/{messageId}")]
    public async Task<ActionResult> DeleteChannelMessage(Guid serverId, Guid messageId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.DeleteChannelMessage(callerId, messageId, serverId);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpDelete("dms/{messageId}")]
    public async Task<ActionResult> DeletePrivateMessage(Guid messageId)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.DeletePrivateMessage(callerId, messageId);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpGet("dms")]
    public async Task<ActionResult<PaginatedConversationsDTO>> GetConversations(
    [FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        currentPage = Math.Max(currentPage, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var result = await _service.GetConversations(callerId, currentPage, pageSize);
        return Ok(result);
    }
}

