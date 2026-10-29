using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Admin.Org;
using Dbvprovas.Ui.Tests.Organization;

namespace Dbvprovas.Ui.Tests.Admin.Org;

public sealed class ClubDirectorTests
{
    private static readonly Guid CandidateMembership = Guid.Parse("00000000-0000-7000-8000-0000000000b1");

    private static void Serve(UiHarness ui, bool hasDirector)
    {
        OrgUi.ServeMe(ui, admin: true);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.AdminClubs, HttpStatusCode.OK, new[]
        {
            new AdminClubResponse(UiHarness.ClubId, "Clube Águias", "Cidade Nova", hasDirector),
        });
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.AdminDirectorCandidates(UiHarness.ClubId), HttpStatusCode.OK, new[]
        {
            new DirectorCandidateResponse(CandidateMembership, "Pessoa Candidata", true),
        });
    }

    // CA-ORG-006 na tela: sem diretor, o admin nomeia um candidato.
    [Fact]
    public void CA_ORG_006_Admin_page_appoints_when_no_director()
    {
        using var ui = new UiHarness();
        Serve(ui, hasDirector: false);
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.AdminAppointDirector(UiHarness.ClubId), HttpStatusCode.NoContent);

        var page = ui.Context.Render<ClubDirector>(p => p.Add(c => c.ClubId, UiHarness.ClubId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.candidate")));
        Assert.Single(page.FindAll("h1"));
        Assert.Empty(page.FindAll("button.replace"));

        page.Find("button.appoint").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.AdminAppointDirector(UiHarness.ClubId)}").Body;
            Assert.Contains(CandidateMembership.ToString(), body);
        });
    }

    // CA-ORG-006 na tela: com diretor, a substituição leva candidato e motivo.
    [Fact]
    public void CA_ORG_006_Admin_page_replaces_with_reason()
    {
        using var ui = new UiHarness();
        Serve(ui, hasDirector: true);
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.AdminReplaceDirector(UiHarness.ClubId), HttpStatusCode.NoContent);

        var page = ui.Context.Render<ClubDirector>(p => p.Add(c => c.ClubId, UiHarness.ClubId));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("button.replace")));
        Assert.Empty(page.FindAll("button.appoint"));
        Assert.True(page.Find("button.replace").HasAttribute("disabled"));

        page.Find("select.candidate").Change(CandidateMembership.ToString());
        page.Find("select.replace-reason").Change(nameof(ReasonCode.DirectorUnavailable));
        page.Find("button.replace").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.AdminReplaceDirector(UiHarness.ClubId)}").Body;
            Assert.Contains(CandidateMembership.ToString(), body);
            Assert.Contains("\"reason\":\"DirectorUnavailable\"", body);
        });
    }

    // A lista de clubes do admin busca pelo texto, na query, e aponta cada clube para a tela da direção.
    [Fact]
    public void Admin_clubs_page_searches_and_links_to_director()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, admin: true);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.AdminClubs, HttpStatusCode.OK, new[]
        {
            new AdminClubResponse(UiHarness.ClubId, "Clube Águias", "Cidade Nova", false),
        });

        var page = ui.Context.Render<Clubs>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.club")));
        Assert.Single(page.FindAll("h1"));
        Assert.Contains("Sem diretor", page.Markup);
        Assert.Equal(UiRoutes.AdminClubDirector(UiHarness.ClubId), page.Find("a.director-link").GetAttribute("href"));

        page.Find("input.club-search").Change("Águias");
        page.Find("button.search").Click();

        page.WaitForAssertion(() => Assert.Contains(ui.Api.Uris, u => u.Contains("?query=")));
    }
}
