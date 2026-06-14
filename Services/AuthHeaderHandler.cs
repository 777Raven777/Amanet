using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ISession _session;
    public AuthHeaderHandler(ISession session) => _session = session;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (_session.Token is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}