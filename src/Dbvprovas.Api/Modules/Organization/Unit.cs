using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Organization;

// Unidade do clube, com sexo, faixa de idade no ano e situação (RN-ORG-013).
public sealed class Unit : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public required string Name { get; set; }
    public Sex Sex { get; set; }
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public UnitStatus Status { get; set; } = UnitStatus.Active;
    public DateTimeOffset CreatedAt { get; init; }
}
