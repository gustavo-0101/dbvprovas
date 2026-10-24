namespace Dbvprovas.Ui.Services.Api;

// Chamadas de login, saída e cadastro.
public sealed class IdentityApi(ApiClient api)
{
    internal ApiClient Api { get; } = api;
}
