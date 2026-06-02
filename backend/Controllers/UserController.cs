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

        public UserController(UserService mainService, TokenService tokenService)
        {
            _mainService = mainService;
            _tokenService = tokenService;
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
            var user = await _mainService.AddUser(request.Email, request.Username, request.Password, request.ProfilePicture);

            var tokenData = await _tokenService.GenerateToken(user.Id);

            var response = new LoginRegisterResponse
            {
                Id = user.Id,
                Username = user.Username,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Token = tokenData.TokenValue
            };

            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(Models.DTO.LoginRequest request)
        {
            UserDTO user = await _mainService.LoginUser(request);

            if (user == null)
            {
                return Unauthorized("Either login or password is invalid");
            }

            var tokenData = await _tokenService.GenerateToken(user.Id);
            var response = new LoginRegisterResponse
            {
                Id = user.Id,
                Username = user.Username,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Token = tokenData.TokenValue
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

    }
}
