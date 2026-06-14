using AvaloniaApplication1.DTO;

namespace AvaloniaApplication1.Services;

public class Session : ISession
{
    public string? Token { get; private set; }
    public void Set(LoginRegisterResponse response) => Token = response.Token;
}