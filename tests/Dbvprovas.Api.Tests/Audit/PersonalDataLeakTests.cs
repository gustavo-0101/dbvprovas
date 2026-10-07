using System.Diagnostics;
using System.Net;
using System.Text;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dbvprovas.Api.Tests.Audit;

public sealed class PersonalDataLeakTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_AUD_004_Personal_values_never_reach_logs_traces_or_errors()
    {
        var sentinel = $"Sentinel {Guid.NewGuid():N}";
        var member = await TestData.CreateClubWithMemberAsync(db, sentinel);
        using var traces = new ActivityCapture();
        var errorBodies = new List<string>();
        await using var dev = new ApiFactory(db, "Development");
        using var client = dev.CreateClientWithToken(await dev.SignInAsync(member.AccountId));
        using var anonymous = dev.CreateClient();

        foreach (var route in new[]
        {
            ApiRoutes.Me, ApiRoutes.Club(member.ClubId), ApiRoutes.Members(member.ClubId),
            ApiRoutes.Member(member.ClubId, member.MembershipId),
        })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await response.Content.ReadAsStringAsync();
        }
        using (var otherClub = await client.GetAsync(ApiRoutes.Members(DevSeed.ClubB)))
        {
            Assert.Equal(HttpStatusCode.NotFound, otherClub.StatusCode);
            errorBodies.Add(await otherClub.Content.ReadAsStringAsync());
        }
        using (var unauthenticated = await anonymous.GetAsync(ApiRoutes.Me))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            errorBodies.Add(await unauthenticated.Content.ReadAsStringAsync());
        }
        using var malformed = new StringContent($"{{\"accountId\": \"{sentinel}\"", Encoding.UTF8, "application/json");
        using (var invalid = await anonymous.PostAsync(ApiRoutes.DevSessions, malformed))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            errorBodies.Add(await invalid.Content.ReadAsStringAsync());
        }
        await using var faulty = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        faulty.ExtraInterceptors.Add(new FailingCommandInterceptor("persons"));
        using var faultyClient = faulty.CreateClient();
        faultyClient.DefaultRequestHeaders.Add(TestAuthHandler.PersonHeader, member.PersonId.ToString());
        using (var failure = await faultyClient.GetAsync(ApiRoutes.Me))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
            errorBodies.Add(await failure.Content.ReadAsStringAsync());
        }

        var logs = dev.Logs.Logs.Concat(faulty.Logs.Logs).ToArray();
        Assert.True(dev.Logs.CreateLogger("Dbvprovas.Tests.CaptureProbe").IsEnabled(LogLevel.Trace));
        foreach (var category in new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql" })
            Assert.Contains(logs, log => log.Category.StartsWith(category, StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Level == LogLevel.Trace);
        Assert.NotEmpty(traces.Items);
        var everything = logs.Select(log => log.Text).Concat(traces.Items).Concat(errorBodies);
        Assert.DoesNotContain(everything, text => text.Contains(sentinel, StringComparison.Ordinal));
    }

    // RNF-PRV-001
    [Fact]
    public async Task Redaction_erases_personal_data_in_logs()
    {
        await using var api = new ApiFactory(db, "Production");
        var logger = api.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Dbvprovas.Tests.Probe");
        var sentinel = $"Sentinel {Guid.NewGuid():N}";
        ProbeLog.PersonNamed(logger, sentinel);

        var probe = Assert.Single(api.Logs.Logs, log => log.Category == "Dbvprovas.Tests.Probe");
        Assert.DoesNotContain(sentinel, probe.Text);
        Assert.Contains("Name=", probe.Text);
        Assert.DoesNotContain("(null)", probe.Text);
        Assert.Equal(string.Empty, Assert.Single(probe.Properties, property => property.Key == "Name").Value);
    }

    // RNF-PRV-001
    [Fact]
    public void Capture_observes_structured_scopes_and_activity_values()
    {
        using var logs = new CapturingLoggerProvider();
        using var traces = new ActivityCapture();
        var logger = logs.CreateLogger("Dbvprovas.Tests.CaptureProbe");
        var scopeSentinel = $"scope-{Guid.NewGuid():N}";
        var stateSentinel = $"state-{Guid.NewGuid():N}";
        using (logger.BeginScope(new Dictionary<string, object?> { ["probe"] = new[] { scopeSentinel } }))
        {
            logger.Log(LogLevel.Trace, default,
                new Dictionary<string, object?> { ["probe"] = new[] { stateSentinel } }, null, (_, _) => "Capture probe");
        }
        Assert.Contains(logs.Texts, text => text.Contains(scopeSentinel, StringComparison.Ordinal));
        Assert.Contains(logs.Texts, text => text.Contains(stateSentinel, StringComparison.Ordinal));

        var baggageSentinel = $"baggage-{Guid.NewGuid():N}";
        var linkSentinel = $"link-{Guid.NewGuid():N}";
        var tagSentinel = $"tag-{Guid.NewGuid():N}";
        var traceStateSentinel = $"tracestate-{Guid.NewGuid():N}";
        using var source = new ActivitySource("Dbvprovas.Tests.CaptureProbe");
        var linkContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded, traceState: $"probe={traceStateSentinel}");
        using (var activity = source.StartActivity("CaptureProbe", ActivityKind.Internal,
            parentContext: default, links: [new ActivityLink(linkContext, new ActivityTagsCollection { ["probe"] = new[] { linkSentinel } })]))
        {
            Assert.NotNull(activity);
            activity.AddBaggage("probe", baggageSentinel);
            activity.SetTag("probe", new[] { tagSentinel });
        }
        foreach (var sentinel in new[] { baggageSentinel, linkSentinel, tagSentinel, traceStateSentinel })
            Assert.Contains(traces.Items, text => text.Contains(sentinel, StringComparison.Ordinal));
    }
}

internal static partial class ProbeLog
{
    [LoggerMessage(LogLevel.Information, "Probe person {Name}")]
    public static partial void PersonNamed(ILogger logger, [PersonalData] string name);
}
