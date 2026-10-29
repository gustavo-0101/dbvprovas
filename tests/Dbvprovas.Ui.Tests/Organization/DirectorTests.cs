using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Organization;

namespace Dbvprovas.Ui.Tests.Organization;

public sealed class DirectorTests
{
    private static readonly Guid OtherMembership = Guid.Parse("00000000-0000-7000-8000-0000000000b1");
    private static readonly Guid NominationId = Guid.Parse("00000000-0000-7000-8000-0000000000d1");

    private static CurrentNominationResponse Pending(Guid nominator, Guid nominee) =>
        new(new NominationResponse(NominationId, nominator, nominee, NominationStatus.Pending, DateTimeOffset.UtcNow.AddDays(7)));

    // CA-ORG-004 na tela: o diretor indica o sucessor e depois cancela a indicação.
    [Fact]
    public void CA_ORG_004_Director_page_nominates_and_cancels()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Director);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.CurrentDirectorNomination(UiHarness.ClubId), HttpStatusCode.OK, new CurrentNominationResponse(null));
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Leaders(UiHarness.ClubId), HttpStatusCode.OK, new[]
        {
            new LeaderResponse(OrgUi.MyMembership, "Pessoa Teste", MembershipStatus.Active, [new LeaderRoleResponse(Guid.NewGuid(), RoleCode.Director, null)]),
            new LeaderResponse(OtherMembership, "Pessoa Líder", MembershipStatus.Active, []),
        });
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.DirectorNominations(UiHarness.ClubId), HttpStatusCode.OK,
            new NominationResponse(NominationId, OrgUi.MyMembership, OtherMembership, NominationStatus.Pending, DateTimeOffset.UtcNow.AddDays(7)));
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.CancelDirectorNomination(UiHarness.ClubId, NominationId), HttpStatusCode.NoContent);

        var page = ui.Context.Render<Director>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("select.nominee")));
        Assert.Single(page.FindAll("h1"));
        Assert.True(page.Find("button.nominate").HasAttribute("disabled"));
        Assert.DoesNotContain(page.FindAll("select.nominee option"), o => o.TextContent == "Pessoa Teste");

        page.Find("select.nominee").Change(OtherMembership.ToString());
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.CurrentDirectorNomination(UiHarness.ClubId), HttpStatusCode.OK, Pending(OrgUi.MyMembership, OtherMembership));
        page.Find("button.nominate").Click();

        page.WaitForAssertion(() => Assert.Contains("Indicação pendente até", page.Markup));
        var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.DirectorNominations(UiHarness.ClubId)}").Body;
        Assert.Contains(OtherMembership.ToString(), body);

        page.Find("button.cancel-nomination").Click();
        page.WaitForAssertion(() => Assert.Contains(ui.Api.Requests, r => r.StartsWith($"POST /{ApiRoutes.CancelDirectorNomination(UiHarness.ClubId, NominationId)}")));
    }

    // CA-ORG-005 na tela: quem foi indicado vê aceitar e recusar, e aceitar chama a rota de aceite.
    [Fact]
    public void CA_ORG_005_Nominee_sees_accept_and_decline()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Secretary);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.CurrentDirectorNomination(UiHarness.ClubId), HttpStatusCode.OK, Pending(OtherMembership, OrgUi.MyMembership));
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.AcceptDirectorNomination(UiHarness.ClubId, NominationId), HttpStatusCode.NoContent);

        var page = ui.Context.Render<Director>();
        page.WaitForAssertion(() => Assert.Contains("Você foi indicado para diretor.", page.Markup));
        Assert.Single(page.FindAll("h1"));
        Assert.Single(page.FindAll("button.decline"));
        Assert.Empty(page.FindAll("select.nominee"));

        page.Find("button.accept").Click();

        page.WaitForAssertion(() => Assert.Contains(ui.Api.Requests, r => r.StartsWith($"POST /{ApiRoutes.AcceptDirectorNomination(UiHarness.ClubId, NominationId)}")));
    }
}
