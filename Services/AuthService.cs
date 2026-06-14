using AvaloniaApplication1.DTO;
using AvaloniaApplication1.Payloads;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ISession _session;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuthService(HttpClient http, ISession session) { _http = http; _session = session; }

    public async Task<AuthResult> LoginAsync(string emailOrUsername, string password, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/User/login",
            new { EmailOrUsername = emailOrUsername, Password = password }, ct);

        if (!resp.IsSuccessStatusCode)
            return new AuthResult(false, await resp.Content.ReadAsStringAsync(ct));

        var dto = await resp.Content.ReadFromJsonAsync<LoginRegisterResponse>(Json, ct);
        _session.Set(dto!);
        return new AuthResult(true, null);
    }

    public async Task<AuthResult> RegisterAsync(RegisterPayload p, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(p.Email),    "Email" },
            { new StringContent(p.Username), "Username" },
            { new StringContent(p.Password), "Password" },
        };

        if (p.AvatarPath is not null)
        {
            var file = new StreamContent(File.OpenRead(p.AvatarPath));
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // or sniff from extension
            form.Add(file, "ProfilePicture", Path.GetFileName(p.AvatarPath)); // ← filename is NOT optional, see below
        }

        using var resp = await _http.PostAsync("api/v1/User/register", form, ct);
        if (!resp.IsSuccessStatusCode)
            return new AuthResult(false, await resp.Content.ReadAsStringAsync(ct));

        var dto = await resp.Content.ReadFromJsonAsync<LoginRegisterResponse>(Json, ct);
        _session.Set(dto!);
        return new AuthResult(true, null);
    }
}