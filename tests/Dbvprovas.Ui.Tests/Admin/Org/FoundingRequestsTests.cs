using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Admin.Org;
using Dbvprovas.Ui.Tests.Organization;

namespace Dbvprovas.Ui.Tests.Admin.Org;

public sealed class FoundingRequestsTests
{
    private static readonly Guid RequestId = Guid.Parse("00000000-0000-7000-8000-0000000000f1");

    private static AdminFoundingRequestResponse Request(bool foundedBefore, bool exception) =>
        new(RequestId, "Clube Novo", "Cidade Nova", "Igreja Central", exception, "Pessoa Fundadora", true, foundedBefore, DateTimeOffset.UtcNow);

    // CA-TEN-010 na tela: quem já fundou só é aprovado com a exceção e o motivo dela.
    [Fact]
    public void CA_TEN_010_Admin_page_approves_with_exception_reason()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, admin: true);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.AdminFoundingRequests, HttpStatusCode.OK, new[] { Request(foundedBefore: true, exception: true) });
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.ApproveFoundingRequest(RequestId), HttpStatusCode.OK, new FoundingApprovedResponse(Guid.NewGuid()));

        var page = ui.Context.Render<FoundingRequests>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.request")));
        Assert.Single(page.FindAll("h1"));
        Assert.Contains("Já fundou", page.Markup);
        Assert.True(page.Find("button.approve").HasAttribute("disabled"));

        page.Find("input.grant-exception").Change(true);
        page.Find("select.exception-reason").Change(nameof(ReasonCode.NewFoundingAfterPrevious));
        Assert.False(page.Find("button.approve").HasAttribute("disabled"));
        page.Find("button.approve").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.ApproveFoundingRequest(RequestId)}").Body;
            Assert.Contains("\"grantException\":true", body);
            Assert.Contains("NewFoundingAfterPrevious", body);
        });
    }

    // CA-TEN-010 na tela: a recusa leva um motivo da lista e só habilita com ele.
    [Fact]
    public void CA_TEN_010_Admin_page_rejects_with_reason()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, admin: true);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.AdminFoundingRequests, HttpStatusCode.OK, new[] { Request(foundedBefore: false, exception: false) });
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.RejectFoundingRequest(RequestId), HttpStatusCode.NoContent);

        var page = ui.Context.Render<FoundingRequests>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.request")));
        Assert.True(page.Find("button.reject").HasAttribute("disabled"));

        page.Find("select.reject-reason").Change(nameof(ReasonCode.DuplicateRequest));
        page.Find("button.reject").Click();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.RejectFoundingRequest(RequestId)}").Body;
            Assert.Contains("\"reason\":\"DuplicateRequest\"", body);
        });
    }

    [Fact]
    public void Admin_page_is_forbidden_for_non_admin()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, admin: false, RoleCode.Director);

        var page = ui.Context.Render<FoundingRequests>();

        page.WaitForAssertion(() => Assert.Contains("Esta área é só do admin da plataforma.", page.Markup));
        Assert.Single(page.FindAll("h1"));
        Assert.DoesNotContain(ui.Api.Requests, r => r.Contains(ApiRoutes.AdminFoundingRequests));
    }
}
