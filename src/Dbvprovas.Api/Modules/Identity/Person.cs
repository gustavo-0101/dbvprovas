using Dbvprovas.Api.Modules.Privacy;

namespace Dbvprovas.Api.Modules.Identity;

// No R0, a pessoa guarda só o nome (D-119, RN-PRV-001).
public sealed class Person
{
    public Guid Id { get; init; }

    [PersonalData]
    public required string Name { get; init; }
}
