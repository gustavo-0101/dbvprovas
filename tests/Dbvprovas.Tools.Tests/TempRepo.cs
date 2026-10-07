using Dbvprovas.TestSupport;

namespace Dbvprovas.Tools.Tests;

internal sealed class TempRepo : IAsyncDisposable
{
    public string Dir { get; } = Directory.CreateTempSubdirectory("pc16-repo-").FullName;

    public static async Task<TempRepo> CreateAsync(string? termsFile = null, bool hooks = false)
    {
        var repo = new TempRepo();
        await repo.GitAsync("init", "-q", "-b", "main");
        await repo.GitAsync("config", "user.email", "ana@example.com");
        await repo.GitAsync("config", "user.name", "Ana Exemplo");
        await repo.GitAsync("config", "commit.gpgsign", "false");
        if (termsFile is not null)
            await repo.GitAsync("config", "pc16.termsFile", termsFile);
        if (hooks)
            await repo.GitAsync("config", "core.hooksPath", Path.Combine(RepoPaths.Root, ".githooks"));
        return repo;
    }

    public async Task WriteAndStageAsync(string path, string content)
    {
        var full = Path.Combine(Dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content);
        await GitAsync("add", path);
    }

    public Task<ProcResult> GitAsync(params string[] args) => Proc.RunAsync("git", args, Dir);

    public ValueTask DisposeAsync()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException)
        {
            // Pasta temporária: se o sistema segurar algum arquivo, ela fica para a limpeza do SO.
        }

        return ValueTask.CompletedTask;
    }
}
