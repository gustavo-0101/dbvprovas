using System.Diagnostics;
using System.Globalization;

namespace Dbvprovas.Api.Modules.Privacy;

// RNF-PRV-001
public static class PrivacyLoggingSetup
{
    public static void ProtectLogInputs(this IServiceCollection services)
    {
        var registration = services.Last(descriptor => descriptor.ServiceType == typeof(ILoggerFactory));
        services.Remove(registration);
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            var inner = registration.ImplementationInstance as ILoggerFactory
                ?? registration.ImplementationFactory?.Invoke(provider) as ILoggerFactory
                ?? (ILoggerFactory)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!);
            return new PrivacyLoggerFactory(inner, registration.ImplementationInstance is null);
        });
    }
}

internal sealed class PrivacyLoggerFactory(ILoggerFactory inner, bool ownsInner) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => new PrivacyLogger(inner.CreateLogger(categoryName));
    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);
    public void Dispose()
    {
        if (ownsInner)
            inner.Dispose();
    }

    private sealed class PrivacyLogger(ILogger innerLogger) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => innerLogger.IsEnabled(logLevel);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            innerLogger.BeginScope(SafeDiagnosticState.Create(state));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var safeException = exception is null ? null : new SafeLoggedException(exception);
            // D-116: o state moderno precisa chegar ao pipeline de classificação intacto.
            if (state is LoggerMessageState)
                innerLogger.Log(logLevel, eventId, state, safeException, formatter);
            else
                innerLogger.Log(logLevel, eventId, SafeDiagnosticState.Create(state), safeException,
                    static (safe, _) => safe.ToString());
        }
    }
}

internal sealed class SafeLoggedException : Exception
{
    private readonly string? _stackTrace;

    public SafeLoggedException(Exception source)
        : base($"An exception of type '{source.GetType().FullName}' occurred.") =>
        _stackTrace = new StackTrace(source, fNeedFileInfo: false).ToString();

    public override string? StackTrace => _stackTrace;
}

// RNF-PRV-001: nenhuma string livre, closure ou state emprestado chega aos providers.
internal sealed class SafeDiagnosticState : List<KeyValuePair<string, object?>>
{
    private static readonly HashSet<string> Fields =
    [
        "Path", "PathBase", "RequestPath", "QueryString", "Host", "Method", "Scheme", "Protocol",
        "ContentType", "ContentLength", "StatusCode", "ElapsedMilliseconds", "DurationMs", "ConnectorId",
        "RequestId", "TraceId", "SpanId", "ParentId", "CommandTimeout", "commandTimeout", "elapsed",
        "HealthStatus", "HealthCheckName", "Duration", "ConnectionId", "CommandId",
    ];
    private const string Redacted = "[redacted]";

    public static SafeDiagnosticState Create(object? input)
    {
        var safe = new SafeDiagnosticState { new("{OriginalFormat}", "Diagnostic event") };
        if (input is IEnumerable<KeyValuePair<string, object?>> fields)
        {
            foreach (var (key, value) in fields)
                if (Fields.Contains(key))
                    safe.Add(new(key, Value(key, value)));
        }
        return safe;
    }

    public override string ToString() => "Diagnostic event " + string.Join(' ',
        this.Where(field => field.Key != "{OriginalFormat}").Select(field => $"{field.Key}={field.Value}"));

    private static object? Value(string key, object? value) => value switch
    {
        null => null,
        bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            or Guid or DateTime or DateTimeOffset or TimeSpan or Enum => value,
        string text when IsSafeText(key, text) => text,
        _ => Redacted,
    };

    private static bool IsSafeText(string key, string text) => key switch
    {
        "Method" => text is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" or "CONNECT" or "TRACE",
        "Scheme" => text is "http" or "https",
        "Protocol" => text is "HTTP/1.0" or "HTTP/1.1" or "HTTP/2" or "HTTP/3",
        "ContentType" => text is "application/json" or "application/problem+json" or "text/plain",
        "TraceId" => IsHex(text, 32),
        "SpanId" or "ParentId" => IsHex(text, 16),
        "RequestId" => text.Length == 13 && text.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'V'),
        "ConnectionId" or "CommandId" => Guid.TryParseExact(text, "D", out _),
        "elapsed" => double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
        "HealthCheckName" => text == "database",
        _ => false,
    };

    private static bool IsHex(string text, int length) => text.Length == length
        && text.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
