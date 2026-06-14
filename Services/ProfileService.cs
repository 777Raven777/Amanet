using AvaloniaApplication1.DTO;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public interface IProfileService
{
    Task<(bool Success, string? Error, UserDTO? User)> UpdateAsync(ProfileUpdate update, CancellationToken ct = default);
    Task<MeDTO?> GetMeAsync(CancellationToken ct = default);
}

public record ProfileUpdate(
    string? Username, string? Email,
    string? OldPassword, string? NewPassword,
    string? AvatarPath);

public class ProfileService : IProfileService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ProfileService(HttpClient http) => _http = http;

    public async Task<(bool, string?, UserDTO?)> UpdateAsync(ProfileUpdate u, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();

        // Only attach fields that changed — backend PatchProfile patches selectively.
        if (!string.IsNullOrWhiteSpace(u.Username)) form.Add(new StringContent(u.Username), "Username");
        if (!string.IsNullOrWhiteSpace(u.Email)) form.Add(new StringContent(u.Email), "Email");
        if (!string.IsNullOrEmpty(u.OldPassword)) form.Add(new StringContent(u.OldPassword), "oldPassword");
        if (!string.IsNullOrEmpty(u.NewPassword)) form.Add(new StringContent(u.NewPassword), "newPassword");

        if (u.AvatarPath is not null)
        {
            var file = new StreamContent(File.OpenRead(u.AvatarPath));
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "ProfilePicture", Path.GetFileName(u.AvatarPath));
        }

        using var resp = await _http.PatchAsync("api/v1/User/update-profile", form, ct);
        if (!resp.IsSuccessStatusCode)
            return (false, await resp.Content.ReadAsStringAsync(ct), null);

        var dto = await resp.Content.ReadFromJsonAsync<UserDTO>(Json, ct);
        return (true, null, dto);
    }
    public async Task<MeDTO?> GetMeAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("api/v1/User/me", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<MeDTO>(Json, ct);
    }
}