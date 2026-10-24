namespace Dbvprovas.Api.Infrastructure;

// Texto livre de cadastro (nome, cidade, igreja, unidade; RF-ID-005, RF-TEN-005, RF-ORG-001): corta as pontas e recusa
// vazio obrigatório, texto acima do limite e caractere de controle. Devolve null quando recusa; "" para opcional vazio.
public static class TextInput
{
    public static string? Clean(string? value, int maxLength, bool required = true)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return required ? null : string.Empty;
        return text.Length <= maxLength && !text.Any(char.IsControl) ? text : null;
    }
}
