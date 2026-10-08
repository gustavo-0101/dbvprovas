using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Dbvprovas.Ui.Tests;

// API falsa: responde por método e caminho; "fora do ar" lança como uma falha de rede.
internal sealed class StubApi : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new();
    private readonly Dictionary<string, Task<HttpResponseMessage>> _held = new();

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

    // Corpo cru, para respostas que não são o JSON esperado.
    public void RespondText(HttpMethod method, string path, HttpStatusCode status, string body, string mediaType) =>
        _routes[$"{method} /{path}"] = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

    // Deixa o pedido sem resposta até a tarefa terminar, para observar a tela enquanto carrega.
    public void Hold(HttpMethod method, string path, Task<HttpResponseMessage> response) =>
        _held[$"{method} /{path}"] = response;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        Requests.Add($"{key} {request.Headers.Authorization}");
        if (Offline)
            throw new HttpRequestException("offline");
        if (_held.TryGetValue(key, out var held))
            return held;
        return Task.FromResult(_routes.TryGetValue(key, out var respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
