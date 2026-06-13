using System.Security.Cryptography;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using backend.Models;
using backend.Models.DTO;
using backend.Cache;

namespace backend.Services;

public class TokenService
{
    private readonly AppDbContext _context;
    private readonly ICacheService _cache;

    public TokenService(AppDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<TokenDTO> GetTokenData(string tokenValue)
    {
        var cached = await _cache.GetAsync<TokenDTO>(CacheKeys.Token(tokenValue));
        if (cached != null) return cached;

        var tokenEntity = await _context.Tokens
        .Include(t => t.User)
        .FirstOrDefaultAsync(x => x.TokenValue == tokenValue);

        if (tokenEntity == null || tokenEntity.User == null
            || tokenEntity.User.LastTokenReset > tokenEntity.CreatedAt)
        {
            return null;
        }

        TokenDTO retrievedToken = new TokenDTO
        {
            TokenValue = tokenValue,
            UserId = tokenEntity.UserId,
            Permissions = tokenEntity.Permissions,
        };

        await _cache.SetAsync(CacheKeys.Token(tokenValue), retrievedToken, TimeSpan.FromHours(1));
        return retrievedToken;
    }

    public async Task<TokenDTO> GenerateToken(Guid userId)
    {
        // User id must be checked in methods that call this method
        byte[] RandomBytes = RandomNumberGenerator.GetBytes(32);
        string tokenValue = Convert.ToBase64String(RandomBytes);
        DateTime now = DateTime.UtcNow;

        TokenDTO createdToken;
        
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) throw new InvalidOperationException($"User {userId} not found");

        Token token = new Token
        {
            TokenValue = tokenValue,
            UserId = userId,
            Permissions = user.Permissions,
        };

        if (user.SuspendedUntil >= now)
        {
            createdToken = new SuspendedTokenDTO
            {
                TokenValue = tokenValue,
                UserId = userId,
                SuspensionEndsAt = user.SuspendedUntil,
                SuspensionReason = user.SuspensionReason,
            };
        }
        else
        {
            createdToken = new TokenDTO
            {
                TokenValue = tokenValue,
                UserId = userId,
            };
        }
        _context.Tokens.Add(token);
        await _context.SaveChangesAsync();
        return createdToken;
    }

    public async Task<bool> Logout(Guid userId) 
    {
        DateTime now = DateTime.UtcNow;

        int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"UPDATE ""Users"" SET LastTokenReset={now} WHERE Id={userId}");

        if (rows > 0)
        {
            List<string> tokens = await _context.Tokens.Where(t => t.UserId == userId).Select(t => t.TokenValue).ToListAsync();
            foreach (var token in tokens)
            {
                await _cache.RemoveAsync(CacheKeys.Token(token));
            }
            return true;
        }
        return false; // only happens if something inportant actually broke
    }
}