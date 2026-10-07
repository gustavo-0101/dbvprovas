using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Dbvprovas.TestSupport;
using Microsoft.Playwright;
using Npgsql;

namespace Dbvprovas.Web.E2E;

// Sobe um Postgres de teste e a API de verdade (com o site), em dev, e abre o Chromium.
public sealed class SiteFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private readonly PostgresDatabase _database = new();
    private readonly StringBuilder _apiOutput = new();
    private string[] _secrets = [];
    private Process? _api;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public string BaseUrl { get; private set; } = "";
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("The browser is not started.");

    public async ValueTask InitializeAsync()
    {
        try
        {
            await StartAsync();
        }
        catch
        {
            try
            {
                await DisposeAsync();
            }
            catch
            {
                // A falha da limpeza não pode esconder o erro original da subida.
            }

            throw;
        }
    }

    // Cada passo roda mesmo que o anterior falhe: nem a API nem o contêiner ficam para trás.
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (_browser is not null)
                await _browser.DisposeAsync();
        }
        finally
        {
            try
            {
                _playwright?.Dispose();
            }
            finally
            {
                try
                {
                    StopApi();
                }
                finally
                {
                    await _database.DisposeAsync();
                }
            }
        }
    }

    private void StopApi()
    {
        if (_api is null)
            return;

        try
        {
            if (!_api.HasExited)
            {
                _api.Kill(entireProcessTree: true);
                // O Kill é assíncrono no SO; sem esperar, o processo ainda segura as DLLs do build
                // e o contêiner pode cair com a API viva.
                _api.WaitForExit(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            _api.Dispose();
        }
    }

    private async Task StartAsync()
    {
        await _database.StartAsync();
        BaseUrl = $"http://127.0.0.1:{FreePort()}";

        var app = _database.AppConnectionString;
        var owner = _database.OwnerConnectionString;
        // A saída da API entra nas mensagens de erro; nenhuma credencial pode aparecer nela (PC-10).
        _secrets = [app, owner, new NpgsqlConnectionStringBuilder(app).Password!, new NpgsqlConnectionStringBuilder(owner).Password!];

        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoPaths.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "run", "--no-build", "-c", Configuration, "--no-launch-profile", "--project", Path.Combine("src", "Dbvprovas.Api"), "--urls", BaseUrl })
            info.ArgumentList.Add(arg);
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["ConnectionStrings__App"] = app;
        info.Environment["ConnectionStrings__Owner"] = owner;

        _api = Process.Start(info) ?? throw new InvalidOperationException("Could not start the API.");
        _api.OutputDataReceived += (_, e) => Append(e.Data);
        _api.ErrorDataReceived += (_, e) => Append(e.Data);
        _api.BeginOutputReadLine();
        _api.BeginErrorReadLine();
        await WaitUntilReadyAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    private void Append(string? line)
    {
        if (line is not null)
        {
            foreach (var secret in _secrets)
                line = line.Replace(secret, "***");
        }

        lock (_apiOutput)
            _apiOutput.AppendLine(line);
    }

    private string ApiOutput()
    {
        lock (_apiOutput)
            return _apiOutput.ToString();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            if (_api!.HasExited)
                throw new InvalidOperationException($"API exited with code {_api.ExitCode}:\n{ApiOutput()}");
            try
            {
                if ((await http.GetAsync($"{BaseUrl}/health/ready")).IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Ainda subindo.
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"API not ready after 2 minutes:\n{ApiOutput()}");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
