using System.Net;
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
}
