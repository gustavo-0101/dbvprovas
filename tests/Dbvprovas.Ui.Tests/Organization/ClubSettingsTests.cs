using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Organization;

namespace Dbvprovas.Ui.Tests.Organization;

public sealed class ClubSettingsTests
{
    // CA-TEN-011 na tela: salva os dados do clube e gera um novo código de convite.
    [Fact]
    public void CA_TEN_011_Settings_page_saves_and_rotates_invite()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Secretary);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.ClubSettings(UiHarness.ClubId), HttpStatusCode.OK,
            new ClubSettingsResponse("Clube Águias", "Cidade Velha", null, "CODIGO-A"));
        ui.Api.Respond(HttpMethod.Put, ApiRoutes.ClubSettings(UiHarness.ClubId), HttpStatusCode.OK,
            new ClubSettingsResponse("Clube Novo", "Cidade Velha", "Igreja Central", "CODIGO-A"));
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.ClubInviteCode(UiHarness.ClubId), HttpStatusCode.OK,
            new ClubSettingsResponse("Clube Novo", "Cidade Velha", "Igreja Central", "CODIGO-B"));

        var page = ui.Context.Render<ClubSettings>();
        page.WaitForAssertion(() => Assert.Equal("CODIGO-A", page.Find("code.invite-code").TextContent));
        Assert.Single(page.FindAll("h1"));

        page.Find("input.club-name").Change("Clube Novo");
        page.Find("input.club-church").Change("Igreja Central");
        page.Find("form.settings-form").Submit();
        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"PUT /{ApiRoutes.ClubSettings(UiHarness.ClubId)}").Body;
            Assert.Contains("\"name\":\"Clube Novo\"", body);
            Assert.Contains("\"church\":\"Igreja Central\"", body);
        });

        page.Find("button.rotate-invite").Click();
        page.WaitForAssertion(() => Assert.Equal("CODIGO-B", page.Find("code.invite-code").TextContent));
    }

    [Fact]
    public void Settings_page_hides_actions_without_director_or_secretary()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Treasurer);

        var page = ui.Context.Render<ClubSettings>();

        page.WaitForAssertion(() => Assert.Contains("Só a diretoria e a secretaria editam os dados do clube.", page.Markup));
        Assert.Single(page.FindAll("h1"));
        Assert.Empty(page.FindAll("form"));
        Assert.Empty(page.FindAll("button.rotate-invite"));
        Assert.DoesNotContain(ui.Api.Requests, r => r.Contains("/settings"));
    }
}
