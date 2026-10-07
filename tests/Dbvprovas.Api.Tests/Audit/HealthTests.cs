using System.Net;
using Dbvprovas.Api.Tests.Infrastructure;
using Npgsql;

namespace Dbvprovas.Api.Tests.Audit;

public sealed class HealthTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_AUD_005_Health_live_and_ready()
    {
        var unavailable = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "unavailable",
            Username = "unavailable",
            Password = Guid.NewGuid().ToString("N"),
            Timeout = 2,
        };
        await using var up = new ApiFactory(db, "Production");
        await using var down = new ApiFactory(db, "Production", unavailable.ConnectionString);
        using var upClient = up.CreateClient();
        using var downClient = down.CreateClient();
        using var liveUp = await upClient.GetAsync("/health/live");
        using var readyUp = await upClient.GetAsync("/health/ready");
        using var liveDown = await downClient.GetAsync("/health/live");
        using var readyDown = await downClient.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveUp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyUp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, liveDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyDown.StatusCode);
        Assert.Equal("Unhealthy", await readyDown.Content.ReadAsStringAsync());
    }
}
