using Dbvprovas.Api.Modules.Privacy;
using Microsoft.Extensions.Compliance.Redaction;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dbvprovas.Api.Modules.Audit;

public static class ObservabilitySetup
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        // D-115
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
        });
        builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        // D-116
        builder.Logging.EnableRedaction();
        builder.Services.AddRedaction(r => r.SetRedactor<ErasingRedactor>(DataTaxonomy.PersonalData));
        builder.Services.ProtectLogInputs();

        // D-115
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("dbvprovas-api"))
            .WithTracing(t => t.AddAspNetCoreInstrumentation(options =>
            {
                options.EnrichWithHttpRequest = (activity, request) => HttpTracePrivacy.Protect(activity, request.HttpContext, response: false);
                options.EnrichWithHttpResponse = (activity, response) => HttpTracePrivacy.Protect(activity, response.HttpContext, response: true);
            }).AddHttpClientInstrumentation().AddNpgsql())
            .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            telemetry.WithLogging().UseOtlpExporter();
    }
}
