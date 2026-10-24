using Dbvprovas.Api.Infrastructure;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class TextInputTests
{
    // RF-ID-005, RF-TEN-005, RF-ORG-001: pontas cortadas; vazio obrigatório, enorme ou com caractere de controle é recusado; opcional vazio vira "".
    [Theory]
    [InlineData("  Clube Novo  ", 120, true, "Clube Novo")]
    [InlineData("   ", 120, true, null)]
    [InlineData(null, 120, true, null)]
    [InlineData("   ", 120, false, "")]
    [InlineData(null, 120, false, "")]
    [InlineData("Clube\u0007Novo", 120, true, null)]
    [InlineData("Uni\tdade", 60, true, null)]
    public void Text_is_trimmed_and_bounded(string? input, int max, bool required, string? expected) =>
        Assert.Equal(expected, TextInput.Clean(input, max, required));

    [Fact]
    public void Text_longer_than_the_limit_is_refused()
    {
        Assert.Equal(new string('x', 120), TextInput.Clean(new string('x', 120), 120));
        Assert.Null(TextInput.Clean(new string('x', 121), 120));
    }
}
