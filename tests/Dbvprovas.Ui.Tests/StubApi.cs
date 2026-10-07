using System.Net;
using System.Net.Http.Json;

namespace Dbvprovas.Ui.Tests;

// API falsa: responde por método e caminho; "fora do ar" lança como uma falha de rede.
internal sealed class StubApi : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new();

    public bool Offline { get; set; }
    public List<string> Requests { get; } = [];

    public void Respond(HttpMethod method, string path, HttpStatusCode status, object? body = null) =>
        _routes[$"{method} /{path}"] = () =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = JsonContent.Create(body);
            return response;
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        Requests.Add($"{key} {request.Headers.Authorization}");
        if (Offline)
            throw new HttpRequestException("offline");
        return Task.FromResult(_routes.TryGetValue(key, out var respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
