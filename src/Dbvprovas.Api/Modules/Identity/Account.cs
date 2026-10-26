using Dbvprovas.Api.Modules.Privacy;

namespace Dbvprovas.Api.Modules.Identity;

// A conta é o acesso pelo Google e pertence a exatamente uma pessoa (RN-ID-001). É achada pelo sub, nunca pelo e-mail.
// O app não lê GoogleSubject nem Email direto (colunas fechadas no banco): use account_by_google_subject, account_email
// e outbox_recipient_email (RN-PRV-001). Consultas de Account projetam só as outras colunas.
public sealed class Account
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }

    [PersonalData]
    public required string GoogleSubject { get; set; }

    [PersonalData]
    public string? Email { get; set; }

    // Papel de admin da plataforma (RN-AUD-005); só conta com 18 anos ou mais (D-146).
    public bool IsPlatformAdmin { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
