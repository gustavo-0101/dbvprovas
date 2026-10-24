using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Privacy;

// Consentimento próprio do adulto, por clube e versão (RN-PRV-002, S-073). Antes de o clube existir, o
// contexto é o pedido de fundação (S-086). Vigente enquanto RevokedAt e EndedAt são nulos.
public sealed class Consent : IMaybeClubOwned
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public ConsentKind Kind { get; init; } = ConsentKind.OwnParticipation;
    public Guid? ClubId { get; set; }
    public Guid? FoundingRequestId { get; init; }
    public required string TermVersion { get; init; }
    public Guid? ConsentedByAccountId { get; init; }
    public DateTimeOffset GrantedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public EndCause? EndCause { get; set; }
}
