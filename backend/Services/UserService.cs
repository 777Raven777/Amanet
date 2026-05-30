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
        private readonly IFileService _fileService;

        public UserService(AppDbContext context, IPasswordHasher<User> passwordHasher, IFileService fileService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _fileService = fileService;
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

        public async Task<UserDTO> AddUser(string email, string username, string password, IFormFile ProfilePicture)
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

            if (ProfilePicture != null)
            {
                string newFileName = await _fileService.SaveFileAsync(ProfilePicture, [".jpg", ".jpeg", ".png"]);
                user.ProfilePictureUrl = $"/uploads/{newFileName}";
            }

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
        public async Task<(bool Success, string Message, UserDTO? userDTO)> PatchProfile(Guid callerId, PatchProfile request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == callerId);

            if (user == null)
            {
                return (false, "User with this Id was not found", null);
            }

            if (!String.IsNullOrEmpty(request.Username))
            {
                string cleanUsername = request.Username.Trim();
                bool taken = await _context.Users
                    .AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLowerInvariant() && u.Id != callerId);
                if (taken) return (false, "Username is already taken", null);
                user.Username = cleanUsername;
            }
            if (!String.IsNullOrEmpty(request.Email))
            {
                string cleanEmail = request.Email.Trim();
                bool taken = await _context.Users
                    .AnyAsync(u => u.Username.ToLower() == cleanEmail.ToLowerInvariant() && u.Id != callerId);
                if (taken) return (false, "Username is already taken", null);
                user.Email = cleanEmail;
            }
            if (!String.IsNullOrEmpty(request.newPassword)){
                if (String.IsNullOrEmpty(request.oldPassword))
                {
                    return (false, "Old password must be provided when changing password", null);
                }
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.oldPassword);

                if (result == PasswordVerificationResult.Failed)
                {
                    return (false, "Old password is incorrect", null);
                }
                user.PasswordHash = _passwordHasher.HashPassword(user, request.newPassword);
            }

            if (request.ProfilePicture != null)
            {
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                    try
                    {
                        _fileService.DeleteFile(user.ProfilePictureUrl);
                    }
                    catch (FileNotFoundException e)
                    {
                        // log error
                    }
                    catch (Exception e)
                    {
                        return (false, e.Message, null);
                    }

                string newFileName = await _fileService.SaveFileAsync(request.ProfilePicture, [".jpg", ".jpeg", ".png"]);
                user.ProfilePictureUrl = $"/uploads/{newFileName}";
            }

            await _context.SaveChangesAsync();

            var updatedUser = new UserDTO()
            {
                Id = user.Id,
                Username = user.Username,
                ProfilePictureUrl = user.ProfilePictureUrl,
            };

            return (true, "Succesfully updated profile", updatedUser);
        }
    }
}
