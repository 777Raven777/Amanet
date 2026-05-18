using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using backend.Services;
using backend.Models;

namespace backend.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TokenService tokenService)
    {
        // Here need to hit cache first but cache does not exist yet so directly to db
        string userToken = null;
        if (context.WebSockets.IsWebSocketRequest)
        {
            var subProtocolHeader = context.Request.Headers["Sec-WebSocket-Protocol"].ToString();

            if (!string.IsNullOrEmpty(subProtocolHeader) && subProtocolHeader.Contains("access_token"))
            {
                var parts = subProtocolHeader.Split(',');
                if (parts.Length > 1)
                {
                    userToken = parts[1].Trim();
                }
            }
        }
        else
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer "))
            {
                userToken = authHeader.Substring("Bearer ".Length).Trim();
            }
        }
        if (!string.IsNullOrWhiteSpace(userToken))
        {
            var tokenData = await tokenService.GetTokenData(userToken);

            if (tokenData != null) 
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, tokenData.UserId.ToString()),
                };

                foreach (TokenPermissions p in Enum.GetValues<TokenPermissions>())
                {
                    if (tokenData.Permissions.Contains(p))
                    {
                        var claim = new Claim(p.ToString(), "allowed" as string);
                        claims.Add(claim);
                    }
                }

                var identity = new ClaimsIdentity(claims, "CustomAuth");
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid Token.");
                return;
            }
        }
        await _next(context);
    }
}
