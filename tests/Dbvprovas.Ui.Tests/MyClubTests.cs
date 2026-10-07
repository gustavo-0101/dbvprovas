using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages;

namespace Dbvprovas.Ui.Tests;

public sealed class MyClubTests
{
    [Fact]
    public void CA_TEN_007_My_club_shows_retry_when_api_is_down()
    {
        using var ui = new UiHarness();
        ui.Api.Offline = true;

        var page = ui.Context.Render<MyClub>();
        page.WaitForAssertion(() => Assert.Contains("Não foi possível falar com o servidor.", page.Markup));
        Assert.DoesNotContain("offline", page.Markup);

        ui.Api.Offline = false;
        ui.ServeClubAguias();
        page.Find("button.retry").Click();

        page.WaitForAssertion(() => Assert.Equal("Clube Águias", page.Find("h1").TextContent));
    }

    [Fact]
    public void CA_TEN_007_My_club_returns_to_login_on_401()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.Unauthorized);
        ui.Navigation.NavigateTo("club"); // parte de /club, para a volta ao início ser observável

        var page = ui.Context.Render<MyClub>();

        page.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/", ui.Navigation.Uri);
            Assert.Null(ui.Store.Token);
        });
    }

    [Fact]
    public void CA_TEN_007_My_club_shows_not_found_on_404()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, new MeResponse("Aurora", [new ClubSummary(UiHarness.ClubId, "Clube Águias")]));

        var page = ui.Context.Render<MyClub>();

        page.WaitForAssertion(() => Assert.Contains("Clube não encontrado.", page.Markup));
    }

    // Resposta 200 que não serve (página de proxy, corpo nulo) vira "sem servidor", sem detalhe técnico (RF-TEN-004).
    [Theory]
    [InlineData("text/html", "<html>portal de acesso</html>")]
    [InlineData("application/json", "null")]
    public void My_club_shows_retry_when_success_body_is_unusable(string mediaType, string body)
    {
        using var ui = new UiHarness();
        ui.Api.RespondText(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, body, mediaType);

        var page = ui.Context.Render<MyClub>();

        page.WaitForAssertion(() => Assert.Contains("Não foi possível falar com o servidor.", page.Markup));
        Assert.NotNull(page.Find("button.retry"));
        Assert.DoesNotContain("portal", page.Markup);
        Assert.DoesNotContain("Exception", page.Markup);
    }

    // Conta sem participação ativa (RF-TEN-001).
    [Fact]
    public void My_club_without_membership_says_so()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, new MeResponse("Heitor", []));

        var page = ui.Context.Render<MyClub>();

        page.WaitForAssertion(() => Assert.Contains("Você ainda não participa de nenhum clube.", page.Markup));
    }

    [Fact]
    public void CA_ID_002_Switch_user_clears_session_and_goes_to_login()
    {
        using var ui = new UiHarness();
        ui.ServeClubAguias();
        ui.Navigation.NavigateTo("club"); // parte de /club, para a volta ao início ser observável
        var page = ui.Context.Render<MyClub>();
        page.WaitForAssertion(() => Assert.Equal("Clube Águias", page.Find("h1").TextContent));

        page.Find("button.switch-user").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/", ui.Navigation.Uri);
            Assert.Null(ui.Store.Token);
        });
    }

    // Os pedidos levam a sessão guardada (RF-TEN-001).
    [Fact]
    public void My_club_sends_session_token()
    {
        using var ui = new UiHarness();
        ui.ServeClubAguias();

        var page = ui.Context.Render<MyClub>();

        page.WaitForAssertion(() => Assert.Contains("GET /api/me Bearer token-ficticio", ui.Api.Requests));
    }
}
