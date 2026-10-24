using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Notifications;

// Aviso na caixa pessoal (RN-NTF-001, D-145): evento e IDs. O ObjectId identifica a ocorrência.
public sealed class Notification
{
    public Guid Id { get; init; }
    public Guid RecipientPersonId { get; init; }
    public NotificationEvent Event { get; init; }
    public Guid? ClubId { get; init; }
    public Guid ObjectId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReadAt { get; set; }
}
