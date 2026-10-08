using System.Net;
using Dbvprovas.Api.Tests.Infrastructure;

namespace Dbvprovas.Api.Tests.Site;

public sealed class SiteHostingTests(PostgresFixture db)
{
    // RNF-TEN-002, CA-TEN-006, D-112: rota de API desconhecida, com ou sem extensão, é o 404 vazio da
    // API (RF-AUD-002), nunca a página do site.
    [Theory]
    [InlineData("api/nao-existe")]
    [InlineData("api/nao-existe.json")]
    public async Task Unknown_api_path_returns_api_404_not_site_page(string path)
    {
        await using var api = new ApiFactory(db, "Development");

        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    // RNF-TEN-005, D-111, D-112: o site é servido pela API, na mesma origem, sem pré-renderização.
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
