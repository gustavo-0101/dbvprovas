using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Audit.AdminAccess;

// Pedido de acesso do admin a dado pessoal de um clube (RN-AUD-003, D-147, D-156).
public sealed class AdminAccessRequest : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid RequestedByPersonId { get; init; }
    public ReasonCode ReasonCode { get; init; }
    public Guid[] TargetMembershipIds { get; init; } = [];
    public string[] Fields { get; init; } = [];
    public AdminAccessStatus Status { get; set; } = AdminAccessStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public Guid? DecidedByPersonId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public Guid? RevokedByPersonId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
