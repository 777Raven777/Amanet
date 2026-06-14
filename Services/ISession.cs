using AvaloniaApplication1.DTO;

namespace AvaloniaApplication1.Services;

public interface ISession
{
    string? Token { get; }
    void Set(LoginRegisterResponse response);
}
