namespace Dbvprovas.Api.Modules.Tenancy;

// Visão club_directory: clubes ativos para a busca e o convite, só com dados do clube (RN-JOIN-001).
public sealed class ClubDirectoryEntry
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string City { get; init; }
    public string? Church { get; init; }
}
