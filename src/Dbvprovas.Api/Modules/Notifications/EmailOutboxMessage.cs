using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Notifications;

// Fila de saída de e-mail (RN-NTF-001): o endereço é lido da conta só na hora do envio, sem cópia aqui.
public sealed class EmailOutboxMessage
{
    public Guid Id { get; init; }
    public Guid RecipientAccountId { get; init; }
    public EmailTemplate Template { get; init; }
    public Guid ObjectId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
