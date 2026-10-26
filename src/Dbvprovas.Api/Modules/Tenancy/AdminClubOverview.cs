namespace Dbvprovas.Api.Modules.Tenancy;

// Visão admin_club_overview: dados não pessoais do clube e se há diretor vigente (RN-ORG-009, D-148).
public sealed class AdminClubOverview
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string City { get; init; }
    public bool HasDirector { get; init; }
}
