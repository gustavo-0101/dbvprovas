namespace Dbvprovas.Api.Modules.Identity;

// A conta é o acesso e pertence a exatamente uma pessoa (RN-ID-001). No R0, só contas de dev.
public sealed class Account
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
}
