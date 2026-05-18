using backend.Models;
using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class FriendsController : ControllerBase
{
	private readonly FriendService _service;

    public FriendsController(FriendService service)
	{
		_service = service;
	}

	[Authorize(Policy = "CanAddFriends")]
	[HttpPost]
	public async Task<ActionResult> SendRequest(SendRequestDTO sendRequestDTO)
	{
		try
		{
            Guid invitedGuid = Guid.Parse(sendRequestDTO.Id);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid callerId = Guid.Parse(userId);
            (bool success, string msg) = await _service.SendRequest(invitedGuid, callerId);

            if (success) return StatusCode(201, msg);

            else return BadRequest(msg);
        }
		catch (FormatException e)
		{
			return BadRequest("User id was not valid UUID");
		}
    }

    [Authorize(Policy = "CanAddFriends")]
    [HttpPost("{id}/reject-request")]
    public async Task<ActionResult> RejectRequest(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid callerId = Guid.Parse(userId);
        (bool success, string msg) = await _service.RejectRequest(id, callerId);

        if (success) return Ok(msg);
        return BadRequest(msg);
    }

    [Authorize(Policy = "CanAddFriends")]
    [HttpPost("{id}/accept-request")]
    public async Task<ActionResult> AcceptRequest(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid callerId = Guid.Parse(userId);
        (bool success, string msg) = await _service.AcceptRequest(id, callerId);

        if (success) return Ok(msg);
        else return BadRequest(msg);
    }

    [Authorize(Policy = "CanAddFriends")]
    [HttpDelete]
    public async Task<ActionResult> DeleteRequest(string requestId)
    {
        try
        {
            Guid requestGuid = Guid.Parse(requestId);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid callerId = Guid.Parse(userId);
            (bool success, string msg) = await _service.DeleteRequest(requestGuid, callerId);

            if (success) return StatusCode(204);

            else return BadRequest(msg);
        }
        catch (FormatException e)
        {
            return BadRequest("Request id was not valid UUID");
        }
    }

    [Authorize]
    [HttpGet("requests/received")]
    public async Task<ActionResult<IEnumerable<RelationshipDTO>>> GetReceivedRequests([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid callerId = Guid.Parse(userId);

        pageSize = Math.Clamp(pageSize, 1, 50);
        currentPage = Math.Clamp(currentPage, 1, int.MaxValue);

        var received = await _service.GetReceivedRequests(callerId, currentPage, pageSize);

        return Ok(received);
    }

    [Authorize]
    [HttpGet("requests/sent")]
    public async Task<ActionResult<IEnumerable<RelationshipDTO>>> GetSentRequests([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid callerId = Guid.Parse(userId);

        pageSize = Math.Clamp(pageSize, 1, 50);
        currentPage = Math.Clamp(currentPage, 1, int.MaxValue);

        var sent = await _service.GetSentRequests(callerId, currentPage, pageSize);

        return Ok(sent);
    }

    [Authorize]
    [HttpGet("friends-list")]
    public async Task<ActionResult<IEnumerable<FriendDTO>>> FriendsList([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid callerId = Guid.Parse(userId);

        pageSize = Math.Clamp(pageSize, 1, 50);

        var friendsDTOs = await _service.RetrieveActiveRelationship(callerId, currentPage, pageSize);

        return Ok(friendsDTOs);
    }
}
