namespace Dbvprovas.Api.Modules.Identity;

public enum SessionTarget
{
    Web,
    App,
}

// Sessão própria (RF-ID-006, RNF-ID-001, D-172): só a conta, sem cargo, clube nem perfil. O token fica com o cliente;
// aqui ficam o hash dele e o hash do nonce do login, que não vale duas vezes. Vence no prazo absoluto, sem renovação.
public sealed class Session
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public required byte[] TokenHash { get; init; }
    public required byte[] NonceHash { get; init; }
    public SessionTarget Target { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}
