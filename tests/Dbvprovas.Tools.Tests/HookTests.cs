using System.Security.Cryptography;

namespace Dbvprovas.Tools.Tests;

// Exercitam os hooks reais pelo git commit (exigem o gitleaks no PATH).
[Collection(nameof(Pc16Collection))]
public sealed class HookTests
{
    private const string Trailer = "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>";

    // O verificador falha fechado com lista vazia, então os testes usam um termo fictício.
    private const string FictitiousTerm = "Clube Exemplo Proibido";

    [Fact]
    public async Task CA_PRV_002_Pre_commit_blocks_email_in_staged_file()
    {
        var terms = await Pc16.WriteTermsFileAsync(FictitiousTerm);
        await using var repo = await TempRepo.CreateAsync(terms, hooks: true);
        await repo.WriteAndStageAsync("contato.txt", $"fale com {Pc16.RealLookingEmail()}\n");

        var commit = await repo.GitAsync("commit", "-m", "Contato");

        Assert.NotEqual(0, commit.ExitCode);
        Assert.Contains("pc16: contato.txt:1: email-fora-dos-dominios-reservados", commit.Output);
        Assert.DoesNotContain("gmail", commit.Output);
    }

    [Fact]
    public async Task CA_PRV_002_Commit_msg_hook_blocks_email_in_message()
    {
        var terms = await Pc16.WriteTermsFileAsync(FictitiousTerm);
        await using var repo = await TempRepo.CreateAsync(terms, hooks: true);
        await repo.WriteAndStageAsync("nota.txt", "texto comum\n");

        var commit = await repo.GitAsync("commit", "-m", $"Fale com {Pc16.RealLookingEmail()}");

        Assert.NotEqual(0, commit.ExitCode);
        Assert.Contains("pc16: mensagem do commit:1: email-fora-dos-dominios-reservados", commit.Output);
    }

    [Fact]
    public async Task CA_PRV_004_Pre_commit_blocks_secret_without_echoing_it()
    {
        var terms = await Pc16.WriteTermsFileAsync(FictitiousTerm);
        await using var repo = await TempRepo.CreateAsync(terms, hooks: true);
        // Segredo falso gerado na hora, para o teste não versionar segredo.
        var secret = "ghp_" + RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 36);
        await repo.WriteAndStageAsync("config.txt", $"token = {secret}\n");

        var commit = await repo.GitAsync("commit", "-m", "Configuração");

        Assert.NotEqual(0, commit.ExitCode);
        Assert.DoesNotContain(secret, commit.Output);
        // Prova que foi o gitleaks que bloqueou, e não o verificador do PC-16.
        Assert.Contains("leaks found: 1", commit.Output);
        Assert.DoesNotContain("pc16: ", commit.Output);
    }

    // Os dois hooks deixam passar um commit limpo (RNF-PRV-005).
    [Fact]
    public async Task Clean_commit_passes_both_hooks()
    {
        var terms = await Pc16.WriteTermsFileAsync(FictitiousTerm);
        await using var repo = await TempRepo.CreateAsync(terms, hooks: true);
        await repo.WriteAndStageAsync("nota.txt", "Cobre CA-TEN-001\n");

        var commit = await repo.GitAsync("commit", "-m", "Nota (CA-TEN-001)", "-m", Trailer);

        Assert.Equal(0, commit.ExitCode);
    }
}
