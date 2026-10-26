using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Ui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Ui.Tests.Shared;

// A API falsa dos testes de tela (D-104): precisa gravar o que a tela mandou e separar respostas por query.
public sealed class StubApiTests
{
    [Fact]
    public async Task Response_registered_with_the_query_wins_over_the_one_for_the_path()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, "api/x", HttpStatusCode.OK, new[] { "path" });
        ui.Api.Respond(HttpMethod.Get, "api/x?offset=50", HttpStatusCode.OK, new[] { "query" });
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var plain = await api.SendAsync<List<string>>(HttpMethod.Get, "api/x");
        var paged = await api.SendAsync<List<string>>(HttpMethod.Get, "api/x?offset=50");
        var other = await api.SendAsync<List<string>>(HttpMethod.Get, "api/x?offset=100");

        Assert.Equal(["path"], plain.Value);
        Assert.Equal(["query"], paged.Value);
        Assert.Equal(["path"], other.Value);
    }

    [Fact]
    public async Task Held_response_with_the_query_wins_over_the_one_for_the_path_and_waits_for_its_task()
    {
        using var ui = new UiHarness();
        var query = new TaskCompletionSource<HttpResponseMessage>();
        ui.Api.Hold(HttpMethod.Get, "api/x", Task.FromResult(Json(["path"])));
        ui.Api.Hold(HttpMethod.Get, "api/x?offset=50", query.Task);
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var plain = await api.SendAsync<List<string>>(HttpMethod.Get, "api/x");
        var paged = api.SendAsync<List<string>>(HttpMethod.Get, "api/x?offset=50");

        Assert.Equal(["path"], plain.Value);
        Assert.False(paged.IsCompleted);

        query.SetResult(Json(["query"]));
        Assert.Equal(["query"], (await paged).Value);
    }

    [Fact]
    public async Task Uris_keep_the_method_and_the_full_address_of_every_request()
    {
        using var ui = new UiHarness();
        await ui.Context.Services.GetRequiredService<SessionState>().LoadAsync();
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        await api.SendAsync<List<string>>(HttpMethod.Get, "api/x?offset=50&limit=20");
        await api.SendAsync(HttpMethod.Post, "api/y");

        Assert.Equal(["GET /api/x?offset=50&limit=20", "POST /api/y"], ui.Api.Uris);
        Assert.Equal(["GET /api/x Bearer token-ficticio", "POST /api/y Bearer token-ficticio"], ui.Api.Requests);
    }

    [Fact]
    public async Task Bodies_keep_the_body_of_each_request_under_the_method_and_path_key()
    {
        using var ui = new UiHarness();
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        await api.SendAsync<List<string>>(HttpMethod.Get, "api/x");
        await api.SendAsync(HttpMethod.Post, "api/y?force=1", new { name = "Clube Aguias" });
        await api.SendAsync(HttpMethod.Put, "api/z", new { reason = "TemporaryLeave" });

        Assert.Equal(
            [("POST /api/y", "{\"name\":\"Clube Aguias\"}"), ("PUT /api/z", "{\"reason\":\"TemporaryLeave\"}")],
            ui.Api.Bodies);
    }

    // O endereço do pedido chega à API falsa já escapado: a resposta registrada com acento tem de usar a forma escapada.
    [Fact]
    public async Task Query_with_accents_is_compared_already_escaped()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, "api/x", HttpStatusCode.OK, new[] { "path" });
        ui.Api.Respond(HttpMethod.Get, "api/x?q=%C3%A1guias", HttpStatusCode.OK, new[] { "escaped" });
        ui.Api.Respond(HttpMethod.Get, "api/x?q=águias", HttpStatusCode.OK, new[] { "raw" });
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var result = await api.SendAsync<List<string>>(HttpMethod.Get, "api/x?q=águias");

        Assert.Equal(["escaped"], result.Value);
        Assert.Equal(["GET /api/x?q=%C3%A1guias"], ui.Api.Uris);
    }

    private static HttpResponseMessage Json(string[] body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
