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
    [HttpPost("dms/{id}/sent-message")]
    public async Task<ActionResult<MessageDTO>> SentPrivateMessage(SendMessageRequest request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText, MessageDTO? message) = await _service.SendPrivateMessage(callerId, request);

        if (success)
        {
            return CreatedAtAction(responseText, message);
        }
        return BadRequest(responseText);
    }

    [Authorize(Policy = "CanUseServers")]
    [HttpPost("server-channels/send-message")]
    public async Task<ActionResult<MessageDTO>> SentChannelMessage(Guid id, SendChannelMessageDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText, MessageDTO? message) = await _service.SendChannelMessage(callerId, request);

        if (success)
        {
            return CreatedAtAction(responseText, message);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpGet("dms/{id}/get-messages")]
    public async Task<ActionResult<PaginatedMessagesDTO>> GetPrivateMessages(Guid id, CursorPaginationRequest request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        int pageSize = request.PageSize ?? 10;
        pageSize = Math.Clamp(pageSize, 1, 100);

        (bool success, string responseText, PaginatedMessagesDTO? messages) = await _service.GetPrivateMessages(callerId, id, pageSize, request.CurrentCursor);

        if (success)
        {
            return Ok(messages);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpGet("server-channels/{id}/get-messages")]
    public async Task<ActionResult<PaginatedMessagesDTO>> GetServerChannelMessages(Guid id, CursorPaginationRequest request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        int pageSize = request.PageSize ?? 10;
        pageSize = Math.Clamp(pageSize, 1, 100);

        (bool success, string responseText, PaginatedMessagesDTO? messages) = await _service.GetServerChannelMessages(callerId, id, pageSize, request.CurrentCursor);

        if (success)
        {
            return Ok(messages);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpPatch("server-channels/{id}")]
    public async Task<ActionResult<PaginatedMessagesDTO>> PatchChannelMessage(Guid id, PatchMessageDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.EditMessage(callerId, id, request.Message, false);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpPatch("dms/{id}")]
    public async Task<ActionResult<PaginatedMessagesDTO>> PatchPrivateMessage(Guid id, PatchMessageDTO request)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.EditMessage(callerId, id, request.Message, true);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpDelete("server-channels/{id}")]
    public async Task<ActionResult<PaginatedMessagesDTO>> DeleteChannelMessage(Guid id)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.DeleteMessage(callerId, id, false);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }

    [Authorize]
    [HttpDelete("dms/{id}")]
    public async Task<ActionResult<PaginatedMessagesDTO>> DeletePrivateMessage(Guid id)
    {
        Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        (bool success, string responseText) = await _service.DeleteMessage(callerId, id, true);

        if (success)
        {
            return Ok(responseText);
        }
        return BadRequest(responseText);
    }
}

