#:property InvariantGlobalization=false
// Verificador da fronteira entre o repositório público e o privado (PC-16, D-109, D-121).
//
// Uso, na raiz do repositório:
//   dotnet run --file tools/pc16-check.cs -- staged              arquivos preparados para o commit
//   dotnet run --file tools/pc16-check.cs -- message <arquivo>   mensagem de commit
//   dotnet run --file tools/pc16-check.cs -- all                 todos os arquivos versionados (modo completo)
//   dotnet run --file tools/pc16-check.cs -- text                texto da entrada padrão (título e descrição de PR)
// Com --ci, valem só os padrões estruturais. Sem ele, a lista local (git config pc16.termsFile)
// é obrigatória, e a falta dela barra o commit (falha fechada).
// A saída nunca repete o valor encontrado: só o local e a regra.
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;
var options = args.ToList();
var ci = options.Remove("--ci");
if (options.Count == 0)
    return Usage();

string[] terms = [];
if (!ci)
{
    var termsFile = Git.Run(allowFailure: true, "config", "--get", "pc16.termsFile").Trim();
    if (termsFile.Length == 0 || !File.Exists(termsFile))
    {
        Console.Error.WriteLine("pc16: lista local não configurada. Rode scripts/setup-dev.ps1 -TermsFile <caminho> ou git config pc16.termsFile <caminho>.");
        return 2;
    }

    terms = Checker.LoadTerms(File.ReadAllLines(termsFile, Encoding.UTF8));
    if (terms.Length == 0)
    {
        Console.Error.WriteLine("pc16: lista local vazia. Ela precisa ter ao menos um termo.");
        return 2;
    }
}

var checker = new Checker(terms);
List<Finding> findings;
try
{
    switch (options[0])
    {
        case "staged":
            findings = Git.Lines("diff", "--cached", "--name-only", "--diff-filter=ACMR")
                .SelectMany(path => checker.CheckFile(path, Git.Run("show", $":{path}")))
                .ToList();
            break;
        case "all":
            findings = Git.Lines("ls-files")
                .SelectMany(path => checker.CheckFile(path, File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : ""))
                .ToList();
            break;
        case "message" when options.Count > 1:
            findings = checker.CheckText("mensagem do commit", File.ReadAllText(options[1], Encoding.UTF8)).ToList();
            break;
        case "text":
            using (var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8))
                findings = checker.CheckText("texto", reader.ReadToEnd()).ToList();
            break;
        default:
            return Usage();
    }
}
catch (GitFailure)
{
    // Falha fechada: sem saída do git não há verificação (nunca vira "sem achados").
    Console.Error.WriteLine("pc16: falha ao consultar o git; nada foi verificado.");
    return 2;
}

foreach (var finding in findings)
    Console.Error.WriteLine($"pc16: {finding.Where}:{finding.Line}: {finding.Rule}");
return findings.Count == 0 ? 0 : 1;

static int Usage()
{
    Console.Error.WriteLine("uso: dotnet run --file tools/pc16-check.cs -- staged|all|text|message <arquivo> [--ci]");
    return 2;
}

sealed class GitFailure : Exception;

sealed record Finding(string Where, int Line, string Rule);

sealed partial class Checker(string[] terms)
{
    // Domínios reservados para exemplos e testes (RFC 2606 e RFC 6761).
    private static readonly string[] ReservedDomains = ["example.com", "example.org", "example.net"];
    private static readonly string[] ReservedSuffixes = [".test", ".example", ".invalid", ".localhost"];

    // Endereços técnicos aceitos: trailers de commit e remotos do git.
    private static readonly string[] AllowedAddresses = ["noreply@anthropic.com", "git@github.com"];
    private const string GitHubNoReply = "users.noreply.github.com";

    // Estes caminhos citam os próprios padrões; neles só valem a regra de pasta e a lista local.
    private static readonly string[] SelfPaths = ["tools/pc16-check.cs", "tests/Dbvprovas.Tools.Tests/"];

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@(?<domain>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,})")]
    private static partial Regex Email();

    // Caminhos de docs privados, de forma estrutural (D-121): "docs/…" ou "specs/…" fora de URLs,
    // e os arquivos de instrução de agentes. Os nomes específicos ficam só na lista local.
    // Aceita prefixos relativos (./, ../, ~/) e as duas barras; URLs são removidas antes (ver Url).
    [GeneratedRegex(@"(?i)(?:(?<![\w/\\.~-])|(?<=(?:^|[\s(\[""'`])(?:(?:\.\.|\.|~)[/\\])+))(?:docs|specs)[/\\][\w.-]+|\b(?:AGENTS|CLAUDE)\.md\b")]
    private static partial Regex PrivateDoc();

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9+.-]*://\S+")]
    private static partial Regex Url();

    public static string[] LoadTerms(IEnumerable<string> lines) =>
        lines.Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(Normalize)
            .Where(term => term.Length > 0)
            .Distinct()
            .ToArray();

    public IEnumerable<Finding> CheckFile(string path, string content)
    {
        var p = path.Replace('\\', '/');
        if (p.StartsWith("docs/", StringComparison.Ordinal) || p.StartsWith("specs/", StringComparison.Ordinal))
            yield return new(p, 0, "pasta-privada");
        else if (PrivateDoc().IsMatch(p))
            yield return new(p, 0, "doc-privado");

        if (content.Contains('\0'))
            yield break; // arquivo binário

        var self = SelfPaths.Any(s => p.StartsWith(s, StringComparison.Ordinal));
        foreach (var finding in CheckText(p, content, structural: !self))
            yield return finding;
    }

    public IEnumerable<Finding> CheckText(string where, string text, bool structural = true)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (structural)
            {
                if (Email().Matches(line).Any(m => !IsAllowed(m.Value, m.Groups["domain"].Value)))
                    yield return new(where, i + 1, "email-fora-dos-dominios-reservados");
                if (PrivateDoc().IsMatch(Url().Replace(line, " ")))
                    yield return new(where, i + 1, "doc-privado");
            }

            if (terms.Length > 0 && terms.Any(Normalize(line).Contains))
                yield return new(where, i + 1, "termo-da-lista-local");
        }
    }

    private static bool IsAllowed(string address, string domain)
    {
        var a = address.ToLowerInvariant();
        var d = domain.ToLowerInvariant();
        return ReservedDomains.Any(r => d == r || d.EndsWith("." + r, StringComparison.Ordinal))
            || ReservedSuffixes.Any(s => d.EndsWith(s, StringComparison.Ordinal))
            || d == GitHubNoReply
            || d.EndsWith("." + GitHubNoReply, StringComparison.Ordinal)
            || AllowedAddresses.Contains(a);
    }

    public static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString();
    }
}

static class Git
{
    public static string Run(params string[] args) => Run(false, args);

    public static string Run(bool allowFailure, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.quotePath=false");
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new GitFailure();
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        error.Wait();
        process.WaitForExit();
        if (process.ExitCode != 0 && !allowFailure)
            throw new GitFailure();
        return output;
    }

    public static IEnumerable<string> Lines(params string[] args) =>
        Run(args).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
