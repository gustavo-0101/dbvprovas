using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Tenancy.Founding;

// Pedido de fundação (RN-TEN-002, D-138): dado da plataforma, visto pelo solicitante e pelo admin.
public sealed class FoundingRequest
{
    public Guid Id { get; init; }
    public Guid RequesterPersonId { get; init; }
    public Guid RequesterAccountId { get; init; }
    public required string ClubName { get; init; }
    public required string ClubCity { get; init; }
    public string? ClubChurch { get; init; }

    // O clube nasce com este ID na aprovação: repetir a aprovação não cria outro (D-140).
    public Guid ProposedClubId { get; init; }
    public FoundingRequestStatus Status { get; set; } = FoundingRequestStatus.Pending;
    public bool ExceptionRequested { get; init; }
    public bool ExceptionGranted { get; set; }
    public ReasonCode? ReasonCode { get; set; }
    public Guid? DecidedByPersonId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; set; }
}
