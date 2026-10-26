using System.Security.Cryptography;

namespace Dbvprovas.Api.Modules.Tenancy;

// Código de convite (RN-JOIN-001, D-162): 8 caracteres de um alfabeto sem 0, 1, I e O, comparado sem
// diferenciar maiúsculas. Nunca vai para log nem para rota (PC-03).
public static class InviteCodes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New() => string.Create(8, 0, static (span, _) =>
    {
        for (var i = 0; i < span.Length; i++)
            span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });

    public static string? Normalize(string? code)
    {
        var value = code?.Trim().ToUpperInvariant();
        return value is { Length: 8 } && value.All(Alphabet.Contains) ? value : null;
    }
}
