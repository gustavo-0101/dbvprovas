using System.Text.RegularExpressions;
using Dbvprovas.TestSupport;

namespace Dbvprovas.Api.Tests.Tenancy;

// CA-TEN-001, RNF-TEN-006: a troca explícita de clube só aparece nos caminhos específicos desta lista.
// Caminho novo exige revisão e entra na lista; nunca por uso livre.
public sealed class ClubScopeUsageTests
{
    private static readonly string[] Allowed =
    [
        "src/Dbvprovas.Api/Modules/Tenancy/ClubScope.cs",
        "src/Dbvprovas.Api/Infrastructure/CoreServices.cs",
        "src/Dbvprovas.Api/Modules/Tenancy/Founding/FoundingDecisionService.cs",
        "src/Dbvprovas.Api/Modules/Organization/AdminDirectorService.cs",
        "src/Dbvprovas.Api/Modules/Joining/JoinRequestService.cs",
        "src/Dbvprovas.Api/Modules/Joining/LeaveService.cs",
        "src/Dbvprovas.Api/Modules/Joining/JoinRequestMaintenance.cs",
        "src/Dbvprovas.Api/Modules/Privacy/ConsentService.cs",
        "src/Dbvprovas.Api/Modules/Audit/AdminAccess/AdminAccessService.cs",
    ];

    // CA-TEN-001, RNF-TEN-006: só o filtro de participação e a troca explícita atribuem o clube do contexto. Busca textual de
    // melhor esforço: `context.ClubId = ...` (qualquer nome terminado em context ou Context) e
    // `GetRequiredService<ClubContext>().ClubId = ...`; não pega `==`, inicializador de objeto nem o ClubId de outra entidade.
    private static readonly string[] ClubAssigners =
    [
        "src/Dbvprovas.Api/Modules/Tenancy/ClubMembershipFilter.cs",
        "src/Dbvprovas.Api/Modules/Tenancy/ClubScope.cs",
    ];

    private static readonly Regex ClubAssignment = new(
        @"\b\w*[cC]ontext\.ClubId\s*=(?!=)|GetRequiredService<ClubContext>\(\)\.ClubId\s*=(?!=)",
        RegexOptions.CultureInvariant);

    [Fact]
    public void CA_TEN_001_Club_scope_switch_is_used_only_by_specific_paths()
    {
        var users = SourceFiles()
            .Where(file => file.Text.Contains("ClubScope", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToList();

        Assert.Contains("src/Dbvprovas.Api/Modules/Tenancy/ClubScope.cs", users);
        Assert.All(users, file => Assert.Contains(file, Allowed));
    }

    [Fact]
    public void CA_TEN_001_Club_context_is_assigned_only_by_membership_filter_and_club_switch()
    {
        var assigners = SourceFiles()
            .Where(file => ClubAssignment.IsMatch(file.Text))
            .Select(file => file.Path)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(ClubAssigners, assigners);
    }

    private static IEnumerable<(string Path, string Text)> SourceFiles()
    {
        var source = Path.Combine(RepoPaths.Root, "src");
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Select(file => (Path.GetRelativePath(RepoPaths.Root, file).Replace('\\', '/'), File.ReadAllText(file)));
    }
}
