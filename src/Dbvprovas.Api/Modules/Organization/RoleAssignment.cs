using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Organization;

// Cargo exercido por uma participação (RN-ORG-001 a 006). Vigente enquanto EndedAt é nulo.
public sealed class RoleAssignment : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid MembershipId { get; init; }
    public RoleCode Role { get; init; }
    public Guid? UnitId { get; init; }
    public Guid? AssignedByPersonId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedByPersonId { get; set; }
    public EndCause? EndCause { get; set; }
}
