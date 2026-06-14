using backend.Cache;
using backend.Data;
using backend.Hubs;
using backend.Hubs;
using backend.Middleware;
using backend.Models;
//added for testing purposes 
using backend.Models.DTO;
using backend.Realtime;
using backend.Realtime.ConnectionTracking;
using backend.Realtime.ConnectionTracking;
using backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.FileProviders;
using StackExchange.Redis;
using StackExchange.Redis;
using System;
using System.Data;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<FriendService>();
builder.Services.AddScoped<ServerChannelService>();
builder.Services.AddScoped<ServerService>();
builder.Services.AddScoped<ServerParticipantService>();
builder.Services.AddScoped<RoleService>();

builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<PresenceService>();

builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddTransient<IFileService, FileService>();

builder.Services
    .AddAuthentication("CustomAuth")
    .AddScheme<AuthenticationSchemeOptions, NullAuthHandler>("CustomAuth", _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        nameof(TokenPermissions.CanUseServers),
        policy => policy.RequireClaim(nameof(TokenPermissions.CanUseServers), "allowed"));

    options.AddPolicy(
        nameof(TokenPermissions.CanAddFriends),
        policy => policy.RequireClaim(nameof(TokenPermissions.CanAddFriends), "allowed"));

    options.AddPolicy(
        nameof(TokenPermissions.CanSendDirectMessages),
        policy => policy.RequireClaim(nameof(TokenPermissions.CanSendDirectMessages), "allowed"));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

/* builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = builder.Configuration["Redis:InstanceName"];
});

builder.Services.AddSingleton<ICacheService, RedisCacheService>(); */

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var conn = sp.GetRequiredService<IConfiguration>()["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");
    return ConnectionMultiplexer.Connect(conn);
});

builder.Services.AddSingleton<ICacheService, RedisCacheService>();

builder.Services.AddSingleton<IConnectionTracker, RedisConnectionTracker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "Uploads");
Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/resources"
});


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();

app.UseHttpsRedirection();

//app.UseWebSockets(); // added for debuging

app.UseAuthentication(); // added for debuging

app.UseMiddleware<backend.Middleware.AuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllers();

//for testing


app.MapHub<ChatHub>("/chathub");

// endpoints for testing purposes

app.MapPost("/test-broadcast", async (IHubContext<ChatHub> hubContext) =>
{
    await hubContext.Clients.All.SendAsync("ReceiveMessage", "Server", "Hello from backend!");
    return Results.Ok("Message sent");
});

app.MapPost("/test-upload", async (IFormFile file, IFileService fileService) =>
{
    var fileName = await fileService.SaveFileAsync(file, [".jpg", ".jpeg", ".png"]);
    return Results.Ok(new { fileName, url = $"/resources/{fileName}" });
}).DisableAntiforgery();

app.MapGet("/test-users", async (AppDbContext db) =>
{
    return Results.Ok(await db.Users.ToListAsync());
});

/*app.MapGet("/test-redis", async (ICacheService cache, AppDbContext db) =>
{
    var firstUser = await db.Users.FirstOrDefaultAsync();
    if (firstUser == null)
        return Results.NotFound("No users in database");

    var key = $"user:{firstUser.Id}";
    var cached = await cache.GetAsync<UserDTO>(key);

    if (cached == null)
    {
        var dto = new UserDTO { Id = firstUser.Id, Username = firstUser.Username };
        await cache.SetAsync(key, dto, TimeSpan.FromMinutes(5));
        return Results.Ok(new { user = dto, fromCache = false });
    }

    return Results.Ok(new { user = cached, fromCache = true });
});*/

//Redis health-check
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug/redis-health", async (
        [FromServices] IConnectionMultiplexer redis,
        [FromServices] ICacheService cache,
        [FromServices] IConnectionTracker tracker) =>
    {
        var report = new Dictionary<string, object>();

        // Layer 1: raw multiplexer — can we PING Redis at all?
        try
        {
            var pong = await redis.GetDatabase().PingAsync();
            report["multiplexer"] = new
            {
                ok = true,
                ping_ms = pong.TotalMilliseconds,
                endpoint = redis.GetEndPoints().FirstOrDefault()?.ToString()
            };
        }
        catch (Exception ex)
        {
            report["multiplexer"] = new { ok = false, error = ex.Message };
            return Results.Json(report, statusCode: 503);
        }

        // Layer 2: ICacheService — round-trip a value
        try
        {
            var probeKey = "healthcheck:cache-probe";
            var probeValue = new CacheProbe(DateTime.UtcNow, Guid.NewGuid().ToString());

            await cache.SetAsync(probeKey, probeValue, TimeSpan.FromSeconds(30));
            var roundTripped = await cache.GetAsync<CacheProbe>(probeKey);
            await cache.RemoveAsync(probeKey);
            var afterRemove = await cache.GetAsync<CacheProbe>(probeKey);

            report["cache_service"] = new
            {
                ok = roundTripped != null && afterRemove == null,
                wrote_and_read = roundTripped != null,
                marker_matched = roundTripped?.Marker == probeValue.Marker,
                removed_cleanly = afterRemove == null
            };
        }
        catch (Exception ex)
        {
            report["cache_service"] = new { ok = false, error = ex.Message };
        }

        // Layer 3: IConnectionTracker — full lifecycle test
        try
        {
            var probeUserId = Guid.NewGuid();
            var probeConn = $"healthcheck-conn-{Guid.NewGuid()}";

            await tracker.AddConnectionAsync(probeUserId, probeConn);
            var connsAfterAdd = await tracker.GetConnectionsAsync(probeUserId);
            var onlineAfterAdd = await tracker.IsOnlineAsync(probeUserId);

            await tracker.RemoveConnectionAsync(probeConn);
            var onlineAfterRemove = await tracker.IsOnlineAsync(probeUserId);

            report["connection_tracker"] = new
            {
                ok = connsAfterAdd.Contains(probeConn)
                     && onlineAfterAdd
                     && !onlineAfterRemove,
                add_succeeded = connsAfterAdd.Contains(probeConn),
                online_when_connected = onlineAfterAdd,
                offline_after_disconnect = !onlineAfterRemove
            };
        }
        catch (Exception ex)
        {
            report["connection_tracker"] = new { ok = false, error = ex.Message };
        }

        return Results.Json(report);
    });
}


app.Run();

record CacheProbe(DateTime Ts, string Marker); //redis health-check