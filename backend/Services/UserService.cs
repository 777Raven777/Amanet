using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static backend.Controllers.UserController;

namespace backend.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserDTO>> SearchUsersAsync(string username)
        {
            return await _context.Users
                .Where(x => x.Username.Contains(username))
                .Select(x => new UserDTO { Id = x.Id, Username = x.Username, Email = x.Email })
                .ToListAsync();
        }

        public async Task<UserDTO> AddUser(string email, string username, string password)
        {
            User user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            var created_user = new UserDTO()
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            };
            return created_user;
        }

        public async Task<bool> UniqueEmailAndUsername(string email, string username)
        {
            return !await _context.Users.AnyAsync(o => o.Email == email || o.Username == username);
        }

        public async Task<UserDTO?> LoginUser(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Username == request.EmailOrUsername ||
                                           x.Email == request.EmailOrUsername);

            if (user == null) return null;
            var hasher = new PasswordHasher<User>();

            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return null;
            }

            var created_user = new UserDTO()
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            };
            return created_user;
        }
    }
}
