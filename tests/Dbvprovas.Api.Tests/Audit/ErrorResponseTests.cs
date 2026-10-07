using System.Net;
using System.Text.Json;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.Extensions.Logging;

namespace Dbvprovas.Api.Tests.Audit;

public sealed class ErrorResponseTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_AUD_006_Unexpected_error_returns_problem_with_trace_id()
    {
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        api.ExtraInterceptors.Add(new FailingCommandInterceptor("persons"));
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PersonHeader, DevSeed.AuroraPerson.ToString());

        using var response = await client.GetAsync(ApiRoutes.Me);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        var traceId = problem.RootElement.GetProperty("traceId").GetString()!.Split('-')[1];
        Assert.DoesNotContain("Simulated", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.Contains(api.Logs.Logs, log => log.Level == LogLevel.Error && log.TraceId == traceId);
    }
}
