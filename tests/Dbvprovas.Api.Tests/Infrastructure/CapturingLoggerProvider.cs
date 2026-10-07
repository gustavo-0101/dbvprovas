using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed record CapturedLog(LogLevel Level, string Category, string Text, string? TraceId,
    IReadOnlyList<KeyValuePair<string, string?>> Properties);

// D-116
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyList<CapturedLog> Logs => _logs.ToArray();
    public IReadOnlyList<string> Texts => _logs.Select(l => l.Text).ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = new StringBuilder(category).Append(' ').Append(formatter(state, exception));
            CapturedValues.Append(text.Append(" state="), state);
            owner._scopes.ForEachScope((scope, builder) => CapturedValues.Append(builder.Append(" scope="), scope), text);
            if (exception is not null)
                text.Append(' ').Append(exception);
            var properties = state is IEnumerable<KeyValuePair<string, object?>> structured
                ? structured.Select(property => new KeyValuePair<string, string?>(property.Key,
                    property.Value is null ? null : CapturedValues.Describe(property.Value))).ToArray()
                : [];
            owner._logs.Enqueue(new CapturedLog(logLevel, category, text.ToString(), Activity.Current?.TraceId.ToString(), properties));
        }
    }
}

// RNF-PRV-001: snapshot imediato, sem perder valores estruturados ou coleções.
internal static class CapturedValues
{
    public static string Describe(object value)
    {
        var text = new StringBuilder();
        Append(text, value);
        return text.ToString();
    }

    public static void Append(StringBuilder text, object? value)
    {
        switch (value)
        {
            case string scalar:
                text.Append(scalar);
                break;
            case IEnumerable<KeyValuePair<string, object?>> properties:
                foreach (var (key, item) in properties)
                {
                    text.Append(' ').Append(key).Append('=');
                    Append(text, item);
                }
                break;
            case IEnumerable<KeyValuePair<string, string?>> strings:
                foreach (var (key, item) in strings)
                    text.Append(' ').Append(key).Append('=').Append(item);
                break;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    Append(text.Append(' '), entry.Key);
                    Append(text.Append('='), entry.Value);
                }
                break;
            case IEnumerable collection:
                foreach (var item in collection)
                    Append(text.Append(' '), item);
                break;
            default:
                text.Append(value);
                break;
        }
    }
}
