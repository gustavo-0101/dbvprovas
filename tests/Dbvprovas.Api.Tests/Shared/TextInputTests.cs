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

    // RF-ID-005, RF-TEN-005, RF-ORG-001: o limite vale para o texto já sem as pontas; quebra de linha na ponta é cortada.
    [Fact]
    public void Limit_is_measured_after_trimming_and_edge_line_breaks_are_trimmed()
    {
        var name = new string('x', 120);

        Assert.Equal(name, TextInput.Clean($"   {name}   ", 120));
        Assert.Equal("Nome", TextInput.Clean("Nome\n", 120));
        Assert.Equal("Nome", TextInput.Clean("\r\n Nome \t", 120));
    }

    // RF-ORG-001: opcional vazio vira "", mas opcional acima do limite é recusado como o obrigatório.
    [Fact]
    public void Optional_text_above_the_limit_is_refused_not_emptied()
    {
        Assert.Null(TextInput.Clean(new string('x', 121), 120, required: false));
        Assert.Equal(new string('x', 120), TextInput.Clean(new string('x', 120), 120, required: false));
        Assert.Null(TextInput.Clean("Igreja\u0007Central", 120, required: false));
    }

    // RF-ID-005, RF-TEN-005, RF-ORG-001: caractere invisível de formatação (bidirecional, largura zero, BOM, tag fora do BMP)
    // é recusado no meio e nas pontas do texto, pois o corte das pontas não o remove. Os pontos de código ficam em número
    // para o arquivo não carregar caractere invisível.
    [Theory]
    [InlineData(0x00AD)]
    [InlineData(0x200B)]
    [InlineData(0x200C)]
    [InlineData(0x200D)]
    [InlineData(0x200E)]
    [InlineData(0x200F)]
    [InlineData(0x202A)]
    [InlineData(0x202B)]
    [InlineData(0x202C)]
    [InlineData(0x202D)]
    [InlineData(0x202E)]
    [InlineData(0x2060)]
    [InlineData(0x2066)]
    [InlineData(0x2067)]
    [InlineData(0x2068)]
    [InlineData(0x2069)]
    [InlineData(0xFEFF)]
    [InlineData(0xE0041)]
    public void Invisible_formatting_characters_are_refused_anywhere_in_the_text(int codePoint)
    {
        var invisible = char.ConvertFromUtf32(codePoint);

        foreach (var input in new[] { $"Maria{invisible}Ana", $"{invisible}Maria", $"Maria{invisible}" })
        {
            Assert.Null(TextInput.Clean(input, 120));
            Assert.Null(TextInput.Clean(input, 120, required: false));
        }
    }

    // RF-ID-005, RF-TEN-005, RF-ORG-001: separador de linha ou de parágrafo (U+2028, U+2029) no meio do texto é recusado;
    // na ponta o corte das pontas já o remove, como qualquer espaço em branco.
    [Theory]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    public void Line_and_paragraph_separators_are_refused_inside_the_text(int codePoint)
    {
        var separator = char.ConvertFromUtf32(codePoint);

        Assert.Null(TextInput.Clean($"Maria{separator}Ana", 120));
        Assert.Null(TextInput.Clean($"Maria{separator}Ana", 120, required: false));
        Assert.Equal("Maria", TextInput.Clean($"Maria{separator}", 120));
    }
}
