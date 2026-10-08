using System.Net;
using Bunit.TestDoubles;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Ui.Tests;

// bUnit com a API falsa e a sessão em memória; dados fictícios (PC-04).
internal sealed class UiHarness : IDisposable
{
    public static readonly Guid ClubId = Guid.Parse("00000000-0000-7000-8000-00000000000a");

    public UiHarness(string? token = "token-ficticio")
    {
        Store.Token = token;
        Context.Services.AddDbvprovasUi(new Uri("http://api.test/"), () => Api);
        Context.Services.AddScoped<ISessionStore>(_ => Store);
    }

    public BunitContext Context { get; } = new();
    public StubApi Api { get; } = new();
    public MemorySessionStore Store { get; } = new();
    public NavigationManager Navigation => Context.Services.GetRequiredService<NavigationManager>();

    // A última navegação pedida pela tela; "replace" evita que o Voltar fique preso no clube.
    public NavigationHistory LastNavigation => ((BunitNavigationManager)Navigation).History.First();

    public void ServeClubAguias()
    {
        Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, new MeResponse("Aurora", [new ClubSummary(ClubId, "Clube Águias")]));
        Api.Respond(HttpMethod.Get, ApiRoutes.Club(ClubId), HttpStatusCode.OK, new ClubResponse(ClubId, "Clube Águias"));
        Api.Respond(HttpMethod.Get, ApiRoutes.Members(ClubId), HttpStatusCode.OK, new[]
        {
            new MemberResponse(Guid.NewGuid(), "Aurora"),
            new MemberResponse(Guid.NewGuid(), "Caio"),
            new MemberResponse(Guid.NewGuid(), "Dalva"),
        });
    }

    public void Dispose() => Context.Dispose();
}
