using Dbvprovas.TestSupport;

namespace Dbvprovas.Tools.Tests;

public sealed class RepoPathsTests
{
    // Apoio dos testes do verificador (RNF-PRV-005).
    [Fact]
    public void Root_contains_the_solution() =>
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "Dbvprovas.slnx")));
}
