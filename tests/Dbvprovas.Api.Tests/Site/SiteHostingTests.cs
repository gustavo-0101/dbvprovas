using System.Net;
using Dbvprovas.Api.Tests.Infrastructure;

namespace Dbvprovas.Api.Tests.Site;

public sealed class SiteHostingTests(PostgresFixture db)
{
    // CA-TEN-006, D-112: rota de API desconhecida é o 404 da API, nunca a página do site.
    [Fact]
    public async Task Unknown_api_path_returns_api_404_not_site_page()
    {
        await using var api = new ApiFactory(db, "Development");

        var response = await api.CreateClient().GetAsync("api/nao-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    // O site é servido pela API, na mesma origem, sem pré-renderização (D-111, D-112).
    [Theory]
    [InlineData("")]
    [InlineData("club")]
    public async Task Site_shell_is_served_anonymously_without_prerendering(string path)
    {
        await using var api = new ApiFactory(db, "Development");

        var response = await api.CreateClient().GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("_framework/blazor.web", html);
        Assert.DoesNotContain("<h1", html);
    }
}
