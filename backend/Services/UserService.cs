using backend.Data;
using backend.Models;
using backend.Models.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;

namespace backend.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;

        private readonly IPasswordHasher<User> _passwordHasher;

        public UserService(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<UserDTO>> SearchUsersAsync(string username, Guid callerId, int currentPage, int pageSize)
        {
            return await _context.Users
                .Where(x => x.Username.Contains(username) && x.Id != callerId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new UserDTO { Id = x.Id, Username = x.Username })
                .Skip((currentPage-1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<UserDTO> AddUser(string email, string username, string password, IFormFile ProfilePicture )
        {
            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanUsername = username.Trim();

            User user = new User
            {
                Id = Guid.NewGuid(),
                Username = cleanUsername,
                Email = cleanEmail,
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            var createdUser = new UserDTO()
            {
                Id = user.Id,
                Username = user.Username,
            };
            return createdUser;
        }

        public async Task<bool> UniqueEmailAndUsername(string email, string username)
        {
            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanUsername = username.Trim().ToLowerInvariant();

            return !await _context.Users.AnyAsync(o =>
                    o.Email == cleanEmail ||
                    o.Username.ToLower() == cleanUsername);
        }

        public async Task<UserDTO?> LoginUser(LoginRequest request)
        {
            string loweredInput = request.EmailOrUsername.Trim().ToLowerInvariant();

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                x.Username.ToLower() == loweredInput ||
                x.Email == loweredInput);

            if (user == null) return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                return null;
            }

            var createdUser = new UserDTO()
            {
                Id = user.Id,
                Username = user.Username,
            };
            return createdUser;
        }
    }
}
