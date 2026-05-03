using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _service;

        public UserController(UserService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> SearchUsers(string username)
        {
            var users = await _service.SearchUsersAsync(username);
            return Ok(users);
        }

        [HttpPost]
        public async Task<ActionResult<UserDTO>> Register(Models.RegisterRequest request)
        {

            bool UniquenessCheck = await _service.UniqueEmailAndUsername(request.Email, request.Username);

            if (!UniquenessCheck)
            {
                return BadRequest("Email or username is already taken");
            }

            var user = await _service.AddUser(request.Email, request.Username, request.Password);
            return Ok(user);
        }

        [HttpPost]
        public async Task<ActionResult<UserDTO>> Login(Models.LoginRequest request)
        {
            UserDTO user = await _service.LoginUser(request);

            if (user == null)
            {
                return BadRequest("Either login or password is invalid");
            }

            return Ok(user);
        }
    }
}
