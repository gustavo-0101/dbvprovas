using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Dbvprovas.Api.Tests.Infrastructure;

// D-116
public sealed class ActivityCapture : IDisposable
{
    private readonly ConcurrentQueue<string> _items = new();
    private readonly ActivityListener _listener;

    public ActivityCapture()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _items.Enqueue(Describe(activity)),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<string> Items => _items.ToArray();

    public void Dispose() => _listener.Dispose();

    private static string Describe(Activity activity)
    {
        var text = new StringBuilder();
        Field(text, "operation", activity.OperationName);
        Field(text, "display", activity.DisplayName);
        Field(text, "statusDescription", activity.StatusDescription);
        Field(text, "traceState", activity.TraceStateString);
        Field(text, "tags", activity.TagObjects);
        Field(text, "baggage", activity.Baggage);
        Field(text, "id", activity.Id);
        Field(text, "parentId", activity.ParentId);
        Field(text, "rootId", activity.RootId);
        Field(text, "idFormat", activity.IdFormat);
        Field(text, "traceId", activity.TraceId);
        Field(text, "spanId", activity.SpanId);
        Field(text, "parentSpanId", activity.ParentSpanId);
        Field(text, "flags", activity.ActivityTraceFlags);
        Field(text, "kind", activity.Kind);
        Field(text, "status", activity.Status);
        Field(text, "start", activity.StartTimeUtc);
        Field(text, "duration", activity.Duration);
        Field(text, "recorded", activity.Recorded);
        Field(text, "allDataRequested", activity.IsAllDataRequested);
        Field(text, "stopped", activity.IsStopped);
        Field(text, "remoteParent", activity.HasRemoteParent);
        Context(text, activity.Context);
        Field(text, "sourceName", activity.Source.Name);
        Field(text, "sourceVersion", activity.Source.Version);
        Field(text, "sourceSchema", activity.Source.TelemetrySchemaUrl);
        Field(text, "sourceTags", activity.Source.Tags);
        foreach (var item in activity.Events)
        {
            Field(text, "eventName", item.Name);
            Field(text, "eventTimestamp", item.Timestamp);
            Field(text, "eventTags", item.Tags);
        }
        foreach (var link in activity.Links)
        {
            Context(text, link.Context);
            Field(text, "linkTags", link.Tags);
        }
        return text.ToString();
    }

    private static void Context(StringBuilder text, ActivityContext context)
    {
        Field(text, "contextTraceId", context.TraceId);
        Field(text, "contextSpanId", context.SpanId);
        Field(text, "contextFlags", context.TraceFlags);
        Field(text, "contextTraceState", context.TraceState);
        Field(text, "contextRemote", context.IsRemote);
    }

    private static void Field(StringBuilder text, string name, object? value) =>
        CapturedValues.Append(text.Append(' ').Append(name).Append('='), value);
}
