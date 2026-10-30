using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Founding;
using Dbvprovas.Ui.Tests.Organization;

namespace Dbvprovas.Ui.Tests.Founding;

public sealed class FoundingRequestPageTests
{
    // CA-TEN-009 na tela: com o termo aceito, envia o pedido com a versão do termo e mostra o pendente.
    [Fact]
    public void CA_TEN_009_Founding_page_sends_request_with_consent()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.MyFoundingRequests, HttpStatusCode.OK, Array.Empty<FoundingRequestResponse>());
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.FoundingRequests, HttpStatusCode.OK,
            new FoundingRequestResponse(Guid.NewGuid(), "Clube Novo", "Cidade Nova", null, FoundingRequestStatus.Pending, false, DateTimeOffset.UtcNow));

        var page = ui.Context.Render<FoundingRequestPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("h1")));
        Assert.True(page.Find("button.send").HasAttribute("disabled"));
        page.Find("input.club-name").Change("Clube Novo");
        page.Find("input.club-city").Change("Cidade Nova");
        page.Find(".consent-term input[type=checkbox]").Change(true);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.MyFoundingRequests, HttpStatusCode.OK, new[]
        {
            new FoundingRequestResponse(Guid.NewGuid(), "Clube Novo", "Cidade Nova", null, FoundingRequestStatus.Pending, false, DateTimeOffset.UtcNow),
        });
        page.Find("form.founding-form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Pendente", page.Markup));
        var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.FoundingRequests}").Body;
        Assert.Contains(ConsentTerms.AdultOwnParticipationVersion, body);
        Assert.Contains("\"consentAccepted\":true", body);
    }

    // CA-TEN-009 d na tela: o código do módulo vira texto claro.
    [Fact]
    public void Founding_page_explains_already_founder()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui);
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.MyFoundingRequests, HttpStatusCode.OK, Array.Empty<FoundingRequestResponse>());
        ui.Api.RespondText(HttpMethod.Post, ApiRoutes.FoundingRequests, HttpStatusCode.Forbidden,
            $"{{\"code\":\"{ProblemCodes.FoundingAlreadyFounder}\"}}", "application/problem+json");

        var page = ui.Context.Render<FoundingRequestPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("h1")));
        page.Find("input.club-name").Change("Clube Novo");
        page.Find("input.club-city").Change("Cidade Nova");
        page.Find(".consent-term input[type=checkbox]").Change(true);
        page.Find("form.founding-form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Você já fundou um clube", page.Markup));
    }
}
