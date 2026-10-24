using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed class ApiFactory(
    PostgresFixture db, string environment, string? appConnectionString = null, string? ownerConnectionString = null)
    : WebApplicationFactory<Program>
{
    public CapturingLoggerProvider Logs { get; } = new();
    public List<IInterceptor> ExtraInterceptors { get; } = [];
    public bool UseTestAuthentication { get; init; }
    public FakeTimeProvider? Clock { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseSetting("ConnectionStrings:App", appConnectionString ?? db.AppConnectionString);
        builder.UseSetting("ConnectionStrings:Owner", ownerConnectionString ?? db.OwnerConnectionString);
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(Logs);
            logging.AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace);
        });
        builder.ConfigureTestServices(services =>
        {
            foreach (var interceptor in ExtraInterceptors)
                services.AddSingleton(interceptor);
            if (Clock is not null)
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            if (UseTestAuthentication)
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            }
        });
    }

    // Cliente autenticado como a conta, pela identidade falsa dos testes.
    public HttpClient CreateClientAs(Guid accountId)
    {
        if (!UseTestAuthentication)
            throw new InvalidOperationException("Set UseTestAuthentication = true to use CreateClientAs.");
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AccountHeader, accountId.ToString());
        return client;
    }

    public HttpClient CreateClientAs(TestActor actor) => CreateClientAs(actor.AccountId);

    public HttpClient CreateClientAs(TestPerson person) => CreateClientAs(person.AccountId);

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<string> SignInAsync(Guid accountId)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(ApiRoutes.DevSessions, new DevSessionRequest(accountId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DevSessionResponse>())!.AccessToken;
    }
}
