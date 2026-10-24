namespace Dbvprovas.Ui.Services.Api;

// Chamadas da auditoria e do acesso do admin.
public sealed class AuditApi(ApiClient api)
{
    internal ApiClient Api { get; } = api;
}
