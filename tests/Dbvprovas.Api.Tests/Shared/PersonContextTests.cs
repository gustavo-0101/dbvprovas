using System.Net;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class PersonContextTests(PostgresFixture db)
{
    // 23:30 de 09/05/2026 em Brasília.
    private static readonly DateTimeOffset EveningBeforeBirthday = new(2026, 5, 10, 2, 30, 0, TimeSpan.Zero);

    // D-146, PC-09: conta marcada e pessoa adulta é admin nesta requisição.
    [Fact]
    public async Task Flagged_adult_is_platform_admin()
    {
        var admin = await TestData.CreatePersonAsync(db, "Admin Adulta", isPlatformAdmin: true, birthDate: new DateOnly(1990, 1, 1));
        await using var probe = new ContextProbe(db, new FakeTimeProvider(EveningBeforeBirthday));

        Assert.Equal(new Seen(admin.PersonId, IsPlatformAdmin: true, PassesAdminPolicy: true), await probe.ReadAsync(admin));
    }

    // D-146, RN-ID-002, PC-12: quem faz 18 anos só é admin a partir da meia-noite de Brasília, relido a cada requisição.
    [Fact]
    public async Task Flagged_person_becomes_platform_admin_at_midnight_in_brasilia()
    {
        var admin = await TestData.CreatePersonAsync(db, "Admin Aniversario", isPlatformAdmin: true, birthDate: new DateOnly(2008, 5, 10));
        var clock = new FakeTimeProvider(EveningBeforeBirthday);
        await using var probe = new ContextProbe(db, clock);

        var before = await probe.ReadAsync(admin);
        clock.Advance(TimeSpan.FromHours(1));
        var after = await probe.ReadAsync(admin);

        Assert.Equal(new Seen(admin.PersonId, IsPlatformAdmin: false, PassesAdminPolicy: false), before);
        Assert.Equal(new Seen(admin.PersonId, IsPlatformAdmin: true, PassesAdminPolicy: true), after);
    }

    // D-146, RN-ID-002: sem nascimento, a marca de admin nunca conta.
    [Fact]
    public async Task Flagged_person_without_birth_date_is_never_platform_admin()
    {
        var admin = await TestData.CreatePersonAsync(db, "Admin Sem Data", age: null, isPlatformAdmin: true);
        await using var probe = new ContextProbe(db, new FakeTimeProvider(EveningBeforeBirthday));

        Assert.Equal(new Seen(admin.PersonId, IsPlatformAdmin: false, PassesAdminPolicy: false), await probe.ReadAsync(admin));
    }

    // D-146, PC-09: conta sem a marca não é admin, mesmo adulta.
    [Fact]
    public async Task Unflagged_adult_is_not_platform_admin()
    {
        var person = await TestData.CreatePersonAsync(db, "Pessoa Comum", birthDate: new DateOnly(1990, 1, 1));
        await using var probe = new ContextProbe(db, new FakeTimeProvider(EveningBeforeBirthday));

        Assert.Equal(new Seen(person.PersonId, IsPlatformAdmin: false, PassesAdminPolicy: false), await probe.ReadAsync(person));
    }

    private sealed record Seen(Guid? PersonId, bool IsPlatformAdmin, bool PassesAdminPolicy);

    // Faz uma requisição de verdade pela identidade falsa e lê, antes de a resposta sair, o contexto da pessoa e a política de admin.
    private sealed class ContextProbe : IAsyncDisposable
    {
        private readonly ApiFactory _api;
        private readonly WebApplicationFactory<Program> _host;
        private Seen? _last;

        public ContextProbe(PostgresFixture db, FakeTimeProvider clock)
        {
            _api = new ApiFactory(db, "Production") { UseTestAuthentication = true, Clock = clock };
            _host = _api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new Capture(seen => _last = seen))));
        }

        public async Task<Seen> ReadAsync(TestPerson person)
        {
            _last = null;
            using var client = _host.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.AccountHeader, person.AccountId.ToString());
            using var response = await client.GetAsync(ApiRoutes.Me);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Assert.IsType<Seen>(_last);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.DisposeAsync();
            await _api.DisposeAsync();
        }
    }

    private sealed class Capture(Action<Seen> record) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (http, inner) =>
            {
                http.Response.OnStarting(async () =>
                {
                    var context = http.RequestServices.GetRequiredService<ClubContext>();
                    var policy = await http.RequestServices.GetRequiredService<IAuthorizationService>()
                        .AuthorizeAsync(http.User, Policies.PlatformAdmin);
                    record(new Seen(context.PersonId, context.IsPlatformAdmin, policy.Succeeded));
                });
                await inner(http);
            });
            next(app);
        };
    }
}
