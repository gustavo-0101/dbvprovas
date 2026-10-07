using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages;

namespace Dbvprovas.Ui.Tests;

public sealed class LoginTests
{
    private static readonly DevAccountResponse[] Accounts =
    [
        new(Guid.NewGuid(), "Aurora — Clube Águias"),
        new(Guid.NewGuid(), "Bento — Clube Corujas"),
    ];

    [Fact]
    public void CA_ID_002_Login_lists_dev_accounts_and_signs_in()
    {
        using var ui = new UiHarness(token: null);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.DevAccounts, HttpStatusCode.OK, Accounts);
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.DevSessions, HttpStatusCode.OK, new DevSessionResponse("token-da-aurora", 28800));

        var page = ui.Context.Render<Login>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("ul.accounts button").Count));
        page.FindAll("ul.accounts button")[0].Click();

        page.WaitForAssertion(() => Assert.Equal("http://localhost/club", ui.Navigation.Uri));
        Assert.Equal("token-da-aurora", ui.Store.Token);
        Assert.True(ui.LastNavigation.Options.ReplaceHistoryEntry); // substitui a entrada: o Voltar não fica preso no clube
    }

    [Fact]
    public void CA_ID_001_Login_without_dev_login_says_unavailable()
    {
        using var ui = new UiHarness(token: null); // a API falsa responde 404 em /api/dev/accounts

        var page = ui.Context.Render<Login>();

        page.WaitForAssertion(() => Assert.Contains("Entrar ainda não está disponível neste ambiente.", page.Markup));
        Assert.Empty(page.FindAll("ul.accounts"));
    }

    // API fora do ar quando a tela de entrada abre (RF-TEN-004).
    [Fact]
    public void Login_shows_retry_when_api_is_down()
    {
        using var ui = new UiHarness(token: null);
        ui.Api.Offline = true;

        var page = ui.Context.Render<Login>();
        page.WaitForAssertion(() => Assert.Contains("Não foi possível falar com o servidor.", page.Markup));
        Assert.DoesNotContain("offline", page.Markup);

        ui.Api.Offline = false;
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.DevAccounts, HttpStatusCode.OK, Accounts);
        page.Find("button.retry").Click();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("ul.accounts button").Count));
    }

    // Falha ao criar a sessão: a tela avisa, não guarda sessão e não sai da entrada (RF-TEN-004).
    [Fact]
    public void Login_shows_retry_when_sign_in_fails()
    {
        using var ui = new UiHarness(token: null);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.DevAccounts, HttpStatusCode.OK, Accounts);
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.DevSessions, HttpStatusCode.InternalServerError);

        var page = ui.Context.Render<Login>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("ul.accounts button").Count));
        page.FindAll("ul.accounts button")[0].Click();

        page.WaitForAssertion(() => Assert.Contains("Não foi possível falar com o servidor.", page.Markup));
        Assert.NotNull(page.Find("button.retry"));
        Assert.Null(ui.Store.Token);
        Assert.Equal("http://localhost/", ui.Navigation.Uri);
    }

    // Com sessão guardada, a entrada vai direto para o clube (RF-ID-001).
    [Fact]
    public void Login_with_stored_session_goes_to_club()
    {
        using var ui = new UiHarness();

        var page = ui.Context.Render<Login>();

        page.WaitForAssertion(() => Assert.Equal("http://localhost/club", ui.Navigation.Uri));
        Assert.DoesNotContain(ui.Api.Requests, r => r.StartsWith("GET /api/dev/accounts", StringComparison.Ordinal));
        Assert.True(ui.LastNavigation.Options.ReplaceHistoryEntry); // substitui a entrada: o Voltar não fica preso no clube
    }
}
