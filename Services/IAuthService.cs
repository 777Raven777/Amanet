using AvaloniaApplication1.Payloads;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string emailOrUsername, string password, CancellationToken ct = default);
    Task<AuthResult> RegisterAsync(RegisterPayload payload, CancellationToken ct = default);
}

public record AuthResult(bool Success, string? Error);