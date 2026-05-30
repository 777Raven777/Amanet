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
    public async Task<ActionResult> SentPrivateMessage(SendMessageRequest request)
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
    [HttpPost("servers/{id}/sent-message")]
    public async Task<ActionResult> SentChannelMessage(SendChannelMessageDTO request)
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
    [HttpGet]
    public async Task<ActionResult> GetPrivateMessages()

}

