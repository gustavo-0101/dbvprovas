using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Organization;

namespace Dbvprovas.Ui.Tests.Organization;

public sealed class LeadersTests
{
    private static readonly Guid OtherMembership = Guid.Parse("00000000-0000-7000-8000-0000000000b1");
    private static readonly Guid UnitId = Guid.Parse("00000000-0000-7000-8000-0000000000c1");

    private static LeaderResponse Me() =>
        new(OrgUi.MyMembership, "Pessoa Teste", MembershipStatus.Active, [new LeaderRoleResponse(Guid.NewGuid(), RoleCode.Director, null)]);

    private static LeaderResponse Other(MembershipStatus status) =>
        new(OtherMembership, "Pessoa Líder", status, []);

    private static void Serve(UiHarness ui, params LeaderResponse[] leaders)
    {
        OrgUi.ServeMe(ui, false, RoleCode.Director);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Leaders(UiHarness.ClubId), HttpStatusCode.OK, leaders);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Units(UiHarness.ClubId), HttpStatusCode.OK, new[]
        {
            new UnitResponse(UnitId, "Unidade Sol", Sex.Female, 10, 12, UnitStatus.Active),
        });
    }

    // CA-ORG-003 na tela: o cargo de conselheiro pede a unidade, e a atribuição leva as duas.
    [Fact]
    public void CA_ORG_003_Leaders_page_assigns_counselor_with_unit()
    {
        using var ui = new UiHarness();
        Serve(ui, Me(), Other(MembershipStatus.Active));
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.RoleAssignments(UiHarness.ClubId), HttpStatusCode.OK,
            new RoleAssignmentResponse(Guid.NewGuid(), OtherMembership, RoleCode.Counselor, UnitId));

        var page = ui.Context.Render<Leaders>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("li.leader").Count));
        Assert.Single(page.FindAll("h1"));
        Assert.Empty(page.FindAll("li.leader:nth-child(2) select.unit"));

        page.Find("li.leader:nth-child(2) select.role").Change(nameof(RoleCode.Counselor));
        Assert.True(page.Find("li.leader:nth-child(2) button.assign").HasAttribute("disabled"));
        page.Find("li.leader:nth-child(2) select.unit").Change(UnitId.ToString());
        page.Find("li.leader:nth-child(2) button.assign").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.RoleAssignments(UiHarness.ClubId)}").Body;
            Assert.Contains(OtherMembership.ToString(), body);
            Assert.Contains("\"role\":\"Counselor\"", body);
            Assert.Contains(UnitId.ToString(), body);
        });
    }

    // CA-JOIN-006 na tela: suspender leva um motivo da lista.
    [Fact]
    public void CA_JOIN_006_Leaders_page_suspends_with_reason()
    {
        using var ui = new UiHarness();
        Serve(ui, Me(), Other(MembershipStatus.Active));
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.SuspendMember(UiHarness.ClubId, OtherMembership), HttpStatusCode.NoContent);

        var page = ui.Context.Render<Leaders>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("li.leader").Count));
        Assert.True(page.Find("li.leader:nth-child(2) button.suspend").HasAttribute("disabled"));

        page.Find("li.leader:nth-child(2) select.suspend-reason").Change(nameof(ReasonCode.TemporaryLeave));
        page.Find("li.leader:nth-child(2) button.suspend").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.SuspendMember(UiHarness.ClubId, OtherMembership)}").Body;
            Assert.Contains("\"reason\":\"TemporaryLeave\"", body);
        });
    }

    // CA-JOIN-008 na tela: quem está suspenso só tem a reativação, com motivo.
    [Fact]
    public void CA_JOIN_008_Leaders_page_reactivates_suspended_with_reason()
    {
        using var ui = new UiHarness();
        Serve(ui, Me(), Other(MembershipStatus.Suspended));
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.ReactivateMember(UiHarness.ClubId, OtherMembership), HttpStatusCode.NoContent);

        var page = ui.Context.Render<Leaders>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("li.leader").Count));
        Assert.Empty(page.FindAll("li.leader:nth-child(2) button.suspend"));
        Assert.True(page.Find("li.leader:nth-child(2) button.reactivate").HasAttribute("disabled"));

        page.Find("li.leader:nth-child(2) select.reactivate-reason").Change(nameof(ReasonCode.ReturnAfterLeave));
        page.Find("li.leader:nth-child(2) button.reactivate").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.ReactivateMember(UiHarness.ClubId, OtherMembership)}").Body;
            Assert.Contains("\"reason\":\"ReturnAfterLeave\"", body);
        });
    }

    [Fact]
    public void Leaders_page_hides_director_role_and_own_actions()
    {
        using var ui = new UiHarness();
        Serve(ui, Me(), Other(MembershipStatus.Active));

        var page = ui.Context.Render<Leaders>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("li.leader").Count));

        Assert.DoesNotContain(page.FindAll("li.leader:nth-child(2) select.role option"), o => o.TextContent == "Diretor");
        Assert.NotEmpty(page.FindAll("li.leader:nth-child(2) select.role option"));
        Assert.Empty(page.FindAll("li.leader:nth-child(1) button"));
        Assert.Empty(page.FindAll("li.leader:nth-child(1) select"));
    }
}
