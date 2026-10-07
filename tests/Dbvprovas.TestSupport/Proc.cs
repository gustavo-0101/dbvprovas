using System.Diagnostics;
using System.Text;

namespace Dbvprovas.TestSupport;

public sealed record ProcResult(int ExitCode, string StdOut, string StdErr)
{
    public string Output => StdOut + StdErr;
}

public static class Proc
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static async Task<ProcResult> RunAsync(
        string file,
        IEnumerable<string> args,
        string workingDirectory,
        string? stdin = null,
        IReadOnlyDictionary<string, string>? env = null,
        TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            UseShellExecute = false,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        if (env is not null)
        {
            foreach (var (key, value) in env)
                info.Environment[key] = value;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (stdin is not null)
            await process.StandardInput.WriteAsync(stdin);
        process.StandardInput.Close();

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Estourou o tempo: encerra a árvore de processos para não deixar órfãos.
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            throw new TimeoutException($"Process {file} exceeded the timeout and was killed.");
        }
        return new ProcResult(process.ExitCode, await stdout, await stderr);
    }
}
