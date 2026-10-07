using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Dbvprovas.Api.Tests.Audit;

// RNF-PRV-001: Console.Out é compartilhado no processo.
[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
public sealed class ConsoleCaptureCollection;

[Collection("ConsoleCapture")]
public sealed class HttpPrivacyTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_AUD_004_Urls_never_reach_logs_json_scopes_or_http_spans()
    {
        var sentinel = $"Probe{Guid.NewGuid():N}";
        var escaped = Uri.EscapeDataString(sentinel + " synthetic");
        var percentEncoded = string.Concat(sentinel.Select(character => $"%{(int)character:X2}"));
        var requests = new (string Path, HttpStatusCode Status)[]
        {
            ($"/api/me?name={sentinel}", HttpStatusCode.OK),
            ($"/api/me?{sentinel}=x", HttpStatusCode.OK),
            ($"/api/me?{sentinel}", HttpStatusCode.OK),
            ($"/api/me?name={sentinel}&name={escaped}", HttpStatusCode.OK),
            ($"/api/me?{percentEncoded}=https://example.test/{percentEncoded}", HttpStatusCode.OK),
            ($"/api/clubs/{sentinel}", HttpStatusCode.NotFound),
            ($"/invalid/{escaped}/{sentinel}", HttpStatusCode.NotFound),
            ($"/invalid/{percentEncoded}", HttpStatusCode.NotFound),
        };
        using var json = new StringWriter();
        var original = Console.Out;
        using var traces = new ActivityCapture();
        var captured = new List<CapturedLog>();
        Console.SetOut(TextWriter.Synchronized(json));
        try
        {
            await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
            await using var host = api.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
                logging.AddFilter<ConsoleLoggerProvider>(null, LogLevel.Trace)));
            using var client = host.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.PersonHeader, DevSeed.AuroraPerson.ToString());
            client.DefaultRequestHeaders.Host = sentinel + ".example.test";
            client.DefaultRequestHeaders.Add("User-Agent", sentinel);
            client.DefaultRequestHeaders.Add("traceparent", "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
            client.DefaultRequestHeaders.Add("tracestate", "probe=" + sentinel);
            client.DefaultRequestHeaders.Add("baggage", "probe=" + sentinel);
            foreach (var (path, expected) in requests)
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(expected, response.StatusCode);
                await response.Content.ReadAsStringAsync();
            }
            captured.AddRange(api.Logs.Logs);
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = json.ToString();
        Assert.NotEmpty(output);
        var records = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            Assert.Contains(records, record => record.RootElement.GetProperty("Category").GetString() == "Microsoft.AspNetCore.Hosting.Diagnostics"
                && record.RootElement.GetProperty("EventId").GetInt32() == 1);
            Assert.Contains(records, record => record.RootElement.GetProperty("State").TryGetProperty("StatusCode", out var status) && status.GetInt32() == 404);
            Assert.Contains(records, record => record.RootElement.GetProperty("Scopes").GetArrayLength() > 0);
            var traceId = captured.First(log => log.Category == "Microsoft.AspNetCore.Hosting.Diagnostics" && log.TraceId is { Length: 32 }).TraceId;
            Assert.Contains(records, record => record.RootElement.GetProperty("Scopes").EnumerateArray()
                .Any(scope => scope.TryGetProperty("TraceId", out var trace) && trace.GetString() == traceId));
        }
        finally
        {
            foreach (var record in records)
                record.Dispose();
        }
        foreach (var category in new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql" })
            Assert.Contains(captured, log => log.Category.StartsWith(category, StringComparison.Ordinal));
        Assert.Contains(captured, log => log.Level == LogLevel.Trace);
        Assert.Contains(captured, log => log.Category == "Microsoft.AspNetCore.Hosting.Diagnostics" && log.TraceId is { Length: 32 });
        Assert.Contains(traces.Items, item => item.Contains("http.route=/api/me", StringComparison.Ordinal));
        Assert.Contains(traces.Items, item => item.Contains("http.response.status_code=404", StringComparison.Ordinal));
        Assert.Contains(traces.Items, item => item.Contains("url.path=[unmatched]", StringComparison.Ordinal));
        Assert.DoesNotContain(traces.Items, item => item.Contains("url.query=", StringComparison.Ordinal));
        Assert.DoesNotContain(sentinel, output);
        Assert.DoesNotContain(percentEncoded, output);
        Assert.DoesNotContain(captured, log => log.Text.Contains(sentinel, StringComparison.Ordinal));
        Assert.DoesNotContain(captured, log => log.Text.Contains(percentEncoded, StringComparison.Ordinal));
        Assert.DoesNotContain(traces.Items, item => item.Contains(sentinel, StringComparison.Ordinal));
        Assert.DoesNotContain(traces.Items, item => item.Contains(percentEncoded, StringComparison.Ordinal));
    }

    // RNF-PRV-001
    [Fact]
    public async Task Http_logging_redacts_untrusted_state_scope_formatter_and_exception()
    {
        var sentinel = $"Probe{Guid.NewGuid():N}";
        await using var api = new ApiFactory(db, "Production");
        var logger = api.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.AspNetCore.Tests.PrivacyProbe");
        var exception = new InvalidOperationException(sentinel);
        exception.Data[sentinel] = new[] { sentinel };
        var formatted = false;
        using (logger.BeginScope(new Dictionary<string, object?> { ["RequestPath"] = sentinel }))
        {
            logger.Log(LogLevel.Trace, new EventId(71), new Dictionary<string, object?>
            {
                ["Path"] = sentinel, [sentinel] = new[] { sentinel }, ["StatusCode"] = 404,
            }, exception, (_, error) => { formatted = true; return sentinel + error; });
            logger.Log(LogLevel.Trace, new EventId(72), new OpaqueState(sentinel), exception, (_, _) => sentinel);
        }
        var probes = api.Logs.Logs.Where(log => log.Category == "Microsoft.AspNetCore.Tests.PrivacyProbe").ToArray();
        Assert.Equal(2, probes.Length);
        Assert.DoesNotContain(probes, log => log.Text.Contains(sentinel, StringComparison.Ordinal));
        Assert.Contains(probes, log => log.Text.Contains("StatusCode=404", StringComparison.Ordinal));
        Assert.False(formatted);
    }

    private sealed record OpaqueState(string Value)
    {
        public override string ToString() => Value;
    }
}
