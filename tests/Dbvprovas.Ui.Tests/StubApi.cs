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

    // Falha de transporte de outro tipo, como a que o Android sobe (RNF-TEN-004).
    public Exception? Failure { get; set; }
    public List<string> Requests { get; } = [];

    // Corpo e endereço completo (com a query) de cada pedido, para a tela conferir o que mandou.
    public List<(string Key, string Body)> Bodies { get; } = [];
    public List<string> Uris { get; } = [];

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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var key = $"{request.Method} {uri.AbsolutePath}";
        var exact = $"{request.Method} {uri.PathAndQuery}";
        Requests.Add($"{key} {request.Headers.Authorization}");
        Uris.Add(exact);
        if (request.Content is not null)
            Bodies.Add((key, await request.Content.ReadAsStringAsync(cancellationToken)));
        if (Offline)
            throw new HttpRequestException("offline");
        if (Failure is not null)
            throw Failure;
        // A resposta registrada com a query vence a registrada só pelo caminho.
        if (_held.TryGetValue(exact, out var heldExact))
            return await heldExact;
        if (_held.TryGetValue(key, out var held))
            return await held;
        if (_routes.TryGetValue(exact, out var respondExact))
            return respondExact();
        return _routes.TryGetValue(key, out var respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
