using Dbvprovas.TestSupport;

namespace Dbvprovas.Tools.Tests;

internal static class Pc16
{
    public static string Script { get; } = Path.Combine(RepoPaths.Root, "tools", "pc16-check.cs");

    public static Task<ProcResult> RunAsync(string workingDirectory, string? stdin, params string[] args) =>
        Proc.RunAsync("dotnet", ["run", "--file", Script, "--", .. args], workingDirectory, stdin);

    // Monta o valor proibido em tempo de execução, para o próprio teste não carregá-lo.
    public static string RealLookingEmail() => "nome" + "@" + "gmail" + ".com";

    public static async Task<string> WriteTermsFileAsync(params string[] terms)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("pc16-terms-").FullName, "termos.txt");
        await File.WriteAllLinesAsync(path, ["# lista de teste", .. terms]);
        return path;
    }
}

// Os testes do verificador rodam em série: cada um compila e executa o script.
[CollectionDefinition(nameof(Pc16Collection))]
public sealed class Pc16Collection;
