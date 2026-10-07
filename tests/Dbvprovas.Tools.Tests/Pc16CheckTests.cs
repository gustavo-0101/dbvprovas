namespace Dbvprovas.Tools.Tests;

[Collection(nameof(Pc16Collection))]
public sealed class Pc16CheckTests
{
    private static readonly string Neutral = Path.GetTempPath();

    [Fact]
    public async Task CA_PRV_002_Verifier_blocks_non_reserved_email()
    {
        var result = await Pc16.RunAsync(Neutral, $"contato: {Pc16.RealLookingEmail()}\n", "text", "--ci");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: texto:1: email-fora-dos-dominios-reservados", result.StdErr);
        Assert.DoesNotContain("gmail", result.Output);
    }

    [Fact]
    public async Task CA_PRV_002_Verifier_accepts_reserved_domains_and_noreply_trailers()
    {
        const string text = """
            ana@example.com
            teste@clube.test
            Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
            appium-uiautomator2-driver@8.7.0
            """;

        var result = await Pc16.RunAsync(Neutral, text, "text", "--ci");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task CA_PRV_002_Message_mode_blocks_email_in_commit_message()
    {
        var terms = await Pc16.WriteTermsFileAsync("termo ficticio zzz");
        await using var repo = await TempRepo.CreateAsync(terms);
        var message = Path.Combine(repo.Dir, "MSG");
        await File.WriteAllTextAsync(message, $"Contato {Pc16.RealLookingEmail()}\n");

        var result = await Pc16.RunAsync(repo.Dir, null, "message", message);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: mensagem do commit:1: email-fora-dos-dominios-reservados", result.StdErr);
    }

    [Fact]
    public async Task CA_PRV_003_Verifier_blocks_local_list_term_ignoring_case_and_accents()
    {
        var terms = await Pc16.WriteTermsFileAsync("Clube Exemplo Proibido");
        await using var repo = await TempRepo.CreateAsync(terms);
        await repo.WriteAndStageAsync("nota.txt", "Visita do CLUBE EXÉMPLO PROIBIDO\n");

        var result = await Pc16.RunAsync(repo.Dir, null, "staged");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: nota.txt:1: termo-da-lista-local", result.StdErr);
        Assert.DoesNotContain("exemplo", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CA_PRV_003_Verifier_fails_closed_without_local_list()
    {
        await using var repo = await TempRepo.CreateAsync();
        await repo.WriteAndStageAsync("nota.txt", "texto comum\n");

        var result = await Pc16.RunAsync(repo.Dir, null, "staged");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("lista local não configurada", result.StdErr);
    }

    [Fact]
    public async Task CA_PRV_003_Full_mode_finds_term_in_committed_file()
    {
        var terms = await Pc16.WriteTermsFileAsync("Clube Exemplo Proibido");
        await using var repo = await TempRepo.CreateAsync(terms);
        await repo.WriteAndStageAsync("antigo.txt", "clube exemplo proibido\n");
        await repo.GitAsync("commit", "-q", "-m", "antigo");

        var result = await Pc16.RunAsync(repo.Dir, null, "all");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: antigo.txt:1: termo-da-lista-local", result.StdErr);
    }

    [Fact]
    public async Task CA_PRV_005_Verifier_blocks_files_in_private_doc_folders()
    {
        var terms = await Pc16.WriteTermsFileAsync("termo ficticio zzz");
        await using var repo = await TempRepo.CreateAsync(terms);
        await repo.WriteAndStageAsync("docs/nota.md", "texto comum\n");
        await repo.WriteAndStageAsync("specs/exemplo.md", "texto comum\n");

        var result = await Pc16.RunAsync(repo.Dir, null, "staged");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: docs/nota.md:0: pasta-privada", result.StdErr);
        Assert.Contains("pc16: specs/exemplo.md:0: pasta-privada", result.StdErr);
    }

    [Theory]
    [InlineData("Ver a spec em specs/R9-exemplo")]
    [InlineData("Conferir docs/exemplo.md")]
    [InlineData("Atualizar o AGENTS.md")]
    public async Task CA_PRV_005_Verifier_blocks_private_doc_paths_in_text(string text)
    {
        var result = await Pc16.RunAsync(Neutral, text + "\n", "text", "--ci");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: texto:1: doc-privado", result.StdErr);
    }

    [Theory]
    [InlineData("Cobre CA-TEN-001 e RN-TEN-001 (D-117)")]
    [InlineData("https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction")]
    [InlineData("https://github.com/dotnet/docs/blob/main/README.md")]
    public async Task CA_PRV_005_Verifier_accepts_ids_and_urls(string text)
    {
        var result = await Pc16.RunAsync(Neutral, text + "\n", "text", "--ci");

        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("Ver ../docs/exemplo.md")]
    [InlineData("Ver ./specs/R9/exemplo.md")]
    [InlineData("Ver ~/docs/exemplo.md")]
    [InlineData(@"Ver docs\exemplo.md")]
    public async Task CA_PRV_005_Verifier_blocks_relative_and_windows_doc_paths(string text)
    {
        var result = await Pc16.RunAsync(Neutral, text + "\n", "text", "--ci");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("pc16: texto:1: doc-privado", result.StdErr);
    }

    [Theory]
    [InlineData("Pasta docs/ sem nada depois")]
    [InlineData("https://github.com/dotnet/docs/blob/main/README.md")]
    public async Task CA_PRV_005_Verifier_ignores_bare_folder_and_urls(string text)
    {
        var result = await Pc16.RunAsync(Neutral, text + "\n", "text", "--ci");

        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("staged")]
    [InlineData("all")]
    public async Task CA_PRV_002_Verifier_fails_closed_when_git_fails(string mode)
    {
        var dir = Directory.CreateTempSubdirectory("pc16-nogit-").FullName;
        try
        {
            var result = await Pc16.RunAsync(dir, null, mode, "--ci");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("falha ao consultar o git", result.StdErr);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task CA_PRV_003_Verifier_ignores_term_that_normalizes_to_empty()
    {
        // Um termo só com marca de acento vira vazio ao normalizar; não pode casar com toda linha.
        var terms = await Pc16.WriteTermsFileAsync("́", "termo ficticio zzz");
        await using var repo = await TempRepo.CreateAsync(terms);
        await repo.WriteAndStageAsync("nota.txt", "texto comum\n");

        var result = await Pc16.RunAsync(repo.Dir, null, "staged");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task CA_PRV_003_Verifier_fails_closed_when_local_list_has_no_terms()
    {
        var terms = await Pc16.WriteTermsFileAsync();
        await using var repo = await TempRepo.CreateAsync(terms);
        await repo.WriteAndStageAsync("nota.txt", "texto comum\n");

        var result = await Pc16.RunAsync(repo.Dir, null, "staged");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("lista local vazia", result.StdErr);
    }
}
