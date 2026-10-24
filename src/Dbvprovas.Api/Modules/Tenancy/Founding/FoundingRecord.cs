namespace Dbvprovas.Api.Modules.Tenancy.Founding;

// Mínimo do histórico de fundação (RN-TEN-003, D-167): fica enquanto houver cadastro, sem cópia de perfil.
public sealed class FoundingRecord
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public Guid ClubId { get; init; }
    public DateTimeOffset ApprovedAt { get; init; }
}
