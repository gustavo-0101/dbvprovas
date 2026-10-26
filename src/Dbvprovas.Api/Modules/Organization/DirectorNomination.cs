using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Organization;

// Indicação de diretor (RN-ORG-007, D-132, D-163).
public sealed class DirectorNomination : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid NominatorMembershipId { get; init; }
    public Guid NomineeMembershipId { get; init; }
    public NominationStatus Status { get; set; } = NominationStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ClosedAt { get; set; }
}
