using backend.Models;
using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;

namespace backend.Controllers
{
    // Important INFO: ALL THE METHODS WILL BE REMADE TO CONSIDER AUTH TOKENS, for now this is only for demonstration and will be changed accordingly later, now we consider that we have user token
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _service;

        public UserController(UserService service)
        {
            _service = service;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<User>>> SearchUsers(string username)
        {
            var users = await _service.SearchUsersAsync(username);
            return Ok(users);
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDTO>> Register([FromForm] Models.DTO.RegisterRequest request)
        {

            bool UniquenessCheck = await _service.UniqueEmailAndUsername(request.Email, request.Username);

            if (!UniquenessCheck)
            {
                return BadRequest("Email or username is already taken");
            }

            var error = ValidateImage(request.ProfilePicture);
            if (error != null)
            {
                return error;
            }
            var user = await _service.AddUser(request.Email, request.Username, request.Password, request.ProfilePicture);

            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(Models.DTO.LoginRequest request)
        {
            UserDTO user = await _service.LoginUser(request);

            if (user == null)
            {
                return Unauthorized("Either login or password is invalid");
            }

            return Ok(user);
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
