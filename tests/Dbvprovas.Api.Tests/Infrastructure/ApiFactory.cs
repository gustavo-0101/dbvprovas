using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed class ApiFactory(
    PostgresFixture db, string environment, string? appConnectionString = null, string? ownerConnectionString = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:App", appConnectionString ?? db.AppConnectionString);
        builder.UseSetting("ConnectionStrings:Owner", ownerConnectionString ?? db.OwnerConnectionString);
    }

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
