namespace AvaloniaApplication1.Payloads;

public record RegisterPayload(string Email, string Username, string Password, string? AvatarPath);