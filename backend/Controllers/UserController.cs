using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _mainService;
        private readonly TokenService _tokenService;
        private readonly ServerParticipantService _serverService;

        public UserController(UserService mainService, TokenService tokenService, ServerParticipantService serverService)
        {
            _mainService = mainService;
            _tokenService = tokenService;
            _serverService = serverService;
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<ActionResult<PaginatedUserListDTO>> SearchUsers(string username, [FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid callerId = Guid.Parse(userId);

            pageSize = Math.Clamp(pageSize, 1, 50);
            currentPage = Math.Clamp(currentPage, 1, int.MaxValue);

            var response = await _mainService.SearchUsersAsync(username, callerId, currentPage, pageSize);
            return Ok(response);
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDTO>> Register([FromForm] Models.DTO.RegisterRequest request)
        {

            bool UniquenessCheck = await _mainService.UniqueEmailAndUsername(request.Email, request.Username);

            if (!UniquenessCheck)
            {
                return BadRequest("Email or username is already taken");
            }

            var error = ValidateImage(request.ProfilePicture);
            if (error != null)
            {
                return error;
            }
            (bool success, string msg, UserDTO? user) = await _mainService.AddUser(request.Email, request.Username, request.Password, request.ProfilePicture);

            if (!success) 
            {
                return BadRequest(msg);
            }

            var tokenData = await _tokenService.GenerateToken(user.Id);

            var suspended = tokenData as SuspendedTokenDTO;
            var response = new LoginRegisterResponse
            {
                Id = user.Id,
                Username = user.Username,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Token = tokenData.TokenValue,
                SuspensionReason = suspended?.SuspensionReason,
                SuspendedUntil = suspended?.SuspensionEndsAt,
            };

            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(LoginRequest request)
        {
            UserDTO user = await _mainService.LoginUser(request);

            if (user == null)
            {
                return Unauthorized("Either login or password is invalid");
            }

            var tokenData = await _tokenService.GenerateToken(user.Id);

            var suspended = tokenData as SuspendedTokenDTO;
            var response = new LoginRegisterResponse
            {
                Id = user.Id,
                Username = user.Username,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Token = tokenData.TokenValue,
                SuspensionReason = suspended?.SuspensionReason,
                SuspendedUntil = suspended?.SuspensionEndsAt,
            };

            return Ok(response);
        }

        [Authorize]
        [HttpPatch("update-profile")]
        public async Task<ActionResult<UserDTO>> PatchProfile([FromForm] PatchProfile request) 
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid callerId = Guid.Parse(userId);

            var error = ValidateImage(request.ProfilePicture);
            if (error != null)
            {
                return error;
            }

            (bool success, string msg, UserDTO userDTO) = await _mainService.PatchProfile(callerId, request);

            if (success)
            {
                return Ok(userDTO);
            }
            return BadRequest(msg);
        }

        private ActionResult? ValidateImage(IFormFile? profileimage)
        {
            if (profileimage == null)
            {
                return null; // If no image apply default later
            }
            try
            {
                if (profileimage.Length > 8 * 1024 * 1024)
                {
                    return BadRequest("File size should not exceed 8MB");
                }
                string[] allowedFileExtensions = [".jpg", ".jpeg", ".png"];
                var extension = Path.GetExtension(profileimage.FileName).ToLowerInvariant();

                if (!allowedFileExtensions.Contains(extension))
                {
                    return BadRequest("Image format must be one of those: jpg/jpeg/png");
                }
                return null;
            }
            catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "CanUseServers")]
        [HttpPost("server-invites/{inviteId}/accept-invite")]
        public async Task<ActionResult> AcceptInvite(Guid serverId, Guid inviteId)
        {
            Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            (bool success, string msg) = await _serverService.AcceptInvite(callerId, inviteId);

            if (success)
            {
                return Ok(msg);
            }
            return BadRequest(msg);
        }

        [Authorize(Policy = "CanUseServers")]
        [HttpPost("server-invites/{inviteId}/reject-invite")]
        public async Task<ActionResult> RejectInvite(Guid serverId, Guid inviteId)
        {
            Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            (bool success, string msg) = await _serverService.RejectInvite(callerId, inviteId);

            if (success)
            {
                return StatusCode(204, msg);
            }
            return BadRequest(msg);
        }

        [Authorize(Policy = "CanUseServers")]
        [HttpGet("server-invites")]
        public async Task<ActionResult<ReceivedServerInvitePaginatedDTO?>> ListServerInvites([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 20)
        {
            Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            pageSize = Math.Clamp(pageSize, 1, 50);
            currentPage = Math.Clamp(currentPage, 1, int.MaxValue);

            (bool success, string msg, ReceivedServerInvitePaginatedDTO? invites) = await _serverService.ListReceivedInvites(callerId, currentPage, pageSize);

            if (success)
            {
                return Ok(invites);
            }
            return BadRequest(msg);
        }


        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            bool success = await _tokenService.Logout(callerId);
            
            if (success)
            {
                return StatusCode(204);
            }
            return BadRequest();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<MeDTO>> GetMe()
        {
            Guid callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _mainService.GetMeByIdAsync(callerId);
            return user is null ? NotFound() : Ok(user);
        }
    }
}
