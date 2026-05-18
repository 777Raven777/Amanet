using backend.Cache;
using backend.ChatHub;
using backend.Data;
//added for testing purposes 
using backend.Models.DTO;
using backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.FileProviders;
using System;
using Microsoft.AspNetCore.Identity;
using backend.Models;
using Microsoft.AspNetCore.Authentication;
using backend.Middleware;

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

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = builder.Configuration["Redis:InstanceName"];
});

builder.Services.AddSingleton<ICacheService, RedisCacheService>();

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Uploads")),
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

app.MapGet("/test-redis", async (ICacheService cache, AppDbContext db) =>
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
});

app.Run();
