using backend.ChatHub;
using backend.Data;
using backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();

builder.Services.AddTransient<IFileService, FileService>();

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

app.Run();
