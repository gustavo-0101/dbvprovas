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

    [Fact]
    public void CA_TEN_001_Club_scope_switch_is_used_only_by_specific_paths()
    {
        var source = Path.Combine(RepoPaths.Root, "src");
        var separator = Path.DirectorySeparatorChar;
        var users = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("ClubScope", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepoPaths.Root, file).Replace('\\', '/'))
            .ToList();

        Assert.Contains("src/Dbvprovas.Api/Modules/Tenancy/ClubScope.cs", users);
        Assert.All(users, file => Assert.Contains(file, Allowed));
    }
}
