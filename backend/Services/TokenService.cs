using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using backend.Models;
using backend.Models.DTO;

namespace backend.Services;

public class TokenService
{
    private readonly AppDbContext _context;

    public TokenService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TokenDTO> GetTokenData(string tokenValue)
    {
        var tokenEntity = await _context.Tokens
        .Include(t => t.User)
        .FirstOrDefaultAsync(x => x.TokenValue == tokenValue);

        if (tokenEntity == null || tokenEntity.User == null
            || tokenEntity.User.LastTokenReset > tokenEntity.CreatedAt)
        {
            return null;
        }

        TokenDTO retrievedToken;

        if (tokenEntity.User.SuspendedUntil >= DateTime.UtcNow)
        {
            retrievedToken = new SuspendedTokenDTO
            {
                TokenValue = tokenValue,
                UserId = tokenEntity.User.Id,
                Permissions = tokenEntity.Permissions,
                SuspensionEndsAt = tokenEntity.User.SuspendedUntil,
                SuspensionReason = tokenEntity.User.SuspensionReason,
            };
        }
        else
        {
            retrievedToken = new TokenDTO
            {
                TokenValue = tokenValue,
                UserId = tokenEntity.UserId,
                Permissions = tokenEntity.Permissions,
            };
        }

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
}

