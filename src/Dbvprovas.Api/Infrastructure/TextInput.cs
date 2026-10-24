using System.Globalization;
using System.Text;

namespace Dbvprovas.Api.Infrastructure;

// Texto livre de cadastro (nome, cidade, igreja, unidade; RF-ID-005, RF-TEN-005, RF-ORG-001): corta as pontas e recusa
// vazio obrigatório, texto acima do limite e caractere invisível (controle, formatação como largura zero e marcas
// bidirecionais, separador de linha ou de parágrafo). Devolve null quando recusa; "" para opcional vazio.
public static class TextInput
{
    public static string? Clean(string? value, int maxLength, bool required = true)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return required ? null : string.Empty;
        return text.Length <= maxLength && !text.EnumerateRunes().Any(IsInvisible) ? text : null;
    }

    private static bool IsInvisible(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
