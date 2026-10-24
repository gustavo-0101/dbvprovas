using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Identity;

// A pessoa guarda só o mínimo da RN-PRV-001; a idade é calculada (RN-ID-002). O cadastro se completa depois
// do primeiro login (RF-ID-005), por isso os campos começam vazios.
public sealed class Person
{
    public Guid Id { get; init; }

    [PersonalData]
    public string? Name { get; set; }

    [PersonalData]
    public DateOnly? BirthDate { get; set; }

    [PersonalData]
    public Sex? Sex { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
