using System.Net;
using System.Net.Http.Headers;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Ui.Tests.Shared;

public sealed class ApiClientTests
{
    private const string Path = "api/probe";

    // D-125: 403, 409 e 400 chegam à tela com o código estável; corpo inesperado vira código ausente.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ApiError.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, ApiError.Conflict)]
    [InlineData(HttpStatusCode.BadRequest, ApiError.Invalid)]
    public async Task Problem_responses_carry_the_stable_code(HttpStatusCode status, ApiError expected)
    {
        using var ui = new UiHarness();
        ui.Api.RespondText(HttpMethod.Post, Path, status, "{\"status\":403,\"code\":\"self.action\"}", "application/problem+json");
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var result = await api.SendAsync(HttpMethod.Post, Path);

        Assert.Equal(expected, result.Error);
        Assert.Equal(ProblemCodes.SelfAction, result.ProblemCode);
    }

    [Fact]
    public async Task Problem_without_json_has_no_code_and_no_content_is_ok()
    {
        using var ui = new UiHarness();
        ui.Api.RespondText(HttpMethod.Post, Path, HttpStatusCode.Forbidden, "<html>proxy</html>", "text/html");
        ui.Api.Respond(HttpMethod.Put, Path, HttpStatusCode.NoContent);
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var forbidden = await api.SendAsync(HttpMethod.Post, Path);
        var done = await api.SendAsync(HttpMethod.Put, Path);

        Assert.Equal(ApiError.Forbidden, forbidden.Error);
        Assert.Null(forbidden.ProblemCode);
        Assert.True(done.IsOk);
    }

    // D-125: o 409 com corpo é lido também pelo SendAsync tipado, sem valor.
    [Fact]
    public async Task Typed_send_carries_the_code_of_a_conflict_without_value()
    {
        using var ui = new UiHarness();
        ui.Api.RespondText(HttpMethod.Post, Path, HttpStatusCode.Conflict, "{\"status\":409,\"code\":\"state.conflict\"}", "application/problem+json");
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var result = await api.SendAsync<List<string>>(HttpMethod.Post, Path);

        Assert.False(result.IsOk);
        Assert.Equal(ApiError.Conflict, result.Error);
        Assert.Equal(ProblemCodes.StateConflict, result.ProblemCode);
        Assert.Null(result.Value);
    }

    // D-125: corpo de problema que não traz um "code" textual vira código ausente, sem derrubar a tela.
    [Theory]
    [InlineData("[]")]
    [InlineData("{\"code\": 5}")]
    [InlineData("{\"status\":409}")]
    [InlineData("")]
    public async Task Problem_body_without_a_text_code_has_no_code(string body)
    {
        using var ui = new UiHarness();
        ui.Api.RespondText(HttpMethod.Post, Path, HttpStatusCode.Conflict, body, "application/problem+json");
        var api = ui.Context.Services.GetRequiredService<ApiClient>();

        var result = await api.SendAsync(HttpMethod.Post, Path);

        Assert.Equal(ApiError.Conflict, result.Error);
        Assert.Null(result.ProblemCode);
    }

    // RNF-TEN-004, D-125: charset inválido no Content-Type não pode escapar do cliente nem deixar a resposta aberta.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ApiError.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, ApiError.Conflict)]
    [InlineData(HttpStatusCode.BadRequest, ApiError.Invalid)]
    public async Task Problem_with_invalid_charset_has_no_code_and_is_disposed(HttpStatusCode status, ApiError expected)
    {
        var response = new TrackedResponse(status, InvalidCharset("{\"code\":\"state.conflict\"}", "application/problem+json"));
        var api = ClientFor(response);

        var result = await api.SendAsync(HttpMethod.Post, Path);

        Assert.Equal(expected, result.Error);
        Assert.Null(result.ProblemCode);
        Assert.True(response.Disposed);
    }

    [Fact]
    public async Task Success_with_invalid_charset_is_unavailable_and_is_disposed()
    {
        var response = new TrackedResponse(HttpStatusCode.OK, InvalidCharset("[\"a\"]", "application/json"));
        var api = ClientFor(response);

        var result = await api.SendAsync<List<string>>(HttpMethod.Get, Path);

        Assert.Equal(ApiError.Unavailable, result.Error);
        Assert.Null(result.Value);
        Assert.True(response.Disposed);
    }

    // Corpo com charset que não existe: a leitura como texto ou como JSON lança InvalidOperationException.
    private static StringContent InvalidCharset(string body, string mediaType) =>
        new(body) { Headers = { ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = "nao-existe" } } };

    private static ApiClient ClientFor(HttpResponseMessage response) =>
        new(new HttpClient(new FixedHandler(response)) { BaseAddress = new Uri("http://api.test/") }, new SessionState(new MemorySessionStore()));

    private sealed class FixedHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    // Resposta que marca quando é descartada.
    private sealed class TrackedResponse : HttpResponseMessage
    {
        public TrackedResponse(HttpStatusCode status, HttpContent content)
            : base(status)
        {
            Content = content;
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
