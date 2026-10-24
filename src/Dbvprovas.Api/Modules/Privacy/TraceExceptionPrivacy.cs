using System.Diagnostics;

namespace Dbvprovas.Api.Modules.Privacy;

// RNF-PRV-001: a mensagem de uma exceção pode trazer um valor (o Postgres repete no erro a entrada inválida). Antes de
// qualquer fonte (ASP.NET Core, HttpClient, Npgsql) gravar o evento "exception" num trace, este ouvinte põe só o tipo
// e a pilha sem a mensagem, como nos logs (SafeLoggedException). A descrição do status de erro, onde o Npgsql põe a
// mensagem das exceções que não são do Postgres, fica só se for um SqlState. Ele não cria nem amostra traces, e nasce
// antes do OpenTelemetry, então é avisado do fim de cada trace antes do exportador.
internal sealed class TraceExceptionPrivacy : IHostedService, IDisposable
{
    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = _ => true,
        ExceptionRecorder = Protect,
        ActivityStopped = ProtectStatus,
    };

    public TraceExceptionPrivacy() => ActivitySource.AddActivityListener(_listener);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _listener.Dispose();

    private static void Protect(Activity activity, Exception exception, ref TagList tags)
    {
        var safe = new TagList();
        foreach (var tag in tags)
        {
            if (!tag.Key.StartsWith("exception.", StringComparison.Ordinal))
                safe.Add(tag);
        }

        var logged = new SafeLoggedException(exception);
        safe.Add("exception.type", exception.GetType().FullName);
        safe.Add("exception.message", logged.Message);
        safe.Add("exception.stacktrace", logged.StackTrace);
        tags = safe;
    }

    private static void ProtectStatus(Activity activity)
    {
        if (activity.StatusDescription is { } description && !IsSqlState(description))
            activity.SetStatus(activity.Status);
    }

    private static bool IsSqlState(string text) => text.Length == 5 && text.All(char.IsAsciiLetterOrDigit);
}
