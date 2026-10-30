using System.Net;
using Dbvprovas.Contracts;

namespace Dbvprovas.Ui.Tests.Organization;

// Respostas falsas comuns às telas de fundação e organização (dados fictícios, PC-04).
internal static class OrgUi
{
    public static readonly Guid MyMembership = Guid.Parse("00000000-0000-7000-8000-0000000000a1");

    public static void ServeMe(UiHarness ui, bool admin = false, params RoleCode[] roles) =>
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, new MeResponse("Pessoa Teste",
            roles.Length == 0 && admin ? [] : [new ClubSummary(UiHarness.ClubId, "Clube Águias", MyMembership, roles)], true, true, admin));
}
